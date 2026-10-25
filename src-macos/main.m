#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#include <sys/sysctl.h>
#include <sys/stat.h>
#include <dirent.h>

static NSString *get_human_size(unsigned long long bytes) {
    if (bytes < 1024) return [NSString stringWithFormat:@"%llu B", bytes];
    if (bytes < 1024 * 1024) return [NSString stringWithFormat:@"%.1f KB", (double)bytes / 1024.0];
    if (bytes < 1024 * 1024 * 1024) return [NSString stringWithFormat:@"%.1f MB", (double)bytes / (1024.0 * 1024.0)];
    return [NSString stringWithFormat:@"%.2f GB", (double)bytes / (1024.0 * 1024.0 * 1024.0)];
}

static unsigned long long calculate_dir_size(NSString *path, unsigned long long *fileCount) {
    unsigned long long total = 0;
    NSFileManager *fm = [NSFileManager defaultManager];
    NSDirectoryEnumerator *enumerator = [fm enumeratorAtPath:path];
    NSString *file;
    while ((file = [enumerator nextObject])) {
        NSString *fullPath = [path stringByAppendingPathComponent:file];
        NSDictionary *attrs = [fm attributesOfItemAtPath:fullPath error:nil];
        if (attrs && [attrs[NSFileType] isEqualToString:NSFileTypeRegular]) {
            total += [attrs[NSFileSize] unsignedLongLongValue];
            if (fileCount) (*fileCount)++;
        }
    }
    return total;
}

static NSComparisonResult compare_semver(NSString *v1, NSString *v2) {
    NSString *clean1 = [v1 stringByTrimmingCharactersInSet:[NSCharacterSet characterSetWithCharactersInString:@"vV "]];
    NSString *clean2 = [v2 stringByTrimmingCharactersInSet:[NSCharacterSet characterSetWithCharactersInString:@"vV "]];
    NSArray *parts1 = [clean1 componentsSeparatedByString:@"."];
    NSArray *parts2 = [clean2 componentsSeparatedByString:@"."];
    NSUInteger maxLen = MAX(parts1.count, parts2.count);
    for (NSUInteger i = 0; i < maxLen; i++) {
        NSInteger n1 = (i < parts1.count) ? [parts1[i] integerValue] : 0;
        NSInteger n2 = (i < parts2.count) ? [parts2[i] integerValue] : 0;
        if (n1 < n2) return NSOrderedAscending;
        if (n1 > n2) return NSOrderedDescending;
    }
    return NSOrderedSame;
}

static NSString *json_serialize_safe(id obj) {
    if (!obj) return @"null";
    if ([NSJSONSerialization isValidJSONObject:obj]) {
        NSError *err = nil;
        NSData *d = [NSJSONSerialization dataWithJSONObject:obj options:0 error:&err];
        if (d && !err) {
            return [[NSString alloc] initWithData:d encoding:NSUTF8StringEncoding] ?: @"null";
        }
    }
    @try {
        NSData *d = [NSJSONSerialization dataWithJSONObject:@[obj] options:0 error:nil];
        NSString *arrStr = [[NSString alloc] initWithData:d encoding:NSUTF8StringEncoding];
        if (arrStr && arrStr.length >= 2) {
            return [arrStr substringWithRange:NSMakeRange(1, arrStr.length - 2)];
        }
    } @catch (NSException *e) {
    }
    return @"null";
}

@interface QuantumBridge : NSObject <WKScriptMessageHandler, WKNavigationDelegate>
@property (nonatomic, weak) WKWebView *webView;
@end

@implementation QuantumBridge

- (void)userContentController:(WKUserContentController *)userContentController
      didReceiveScriptMessage:(WKScriptMessage *)message {
    if (![message.body isKindOfClass:[NSDictionary class]]) return;
    NSDictionary *payload = message.body;
    NSString *callId = payload[@"id"] ?: @"";
    NSString *cmd = payload[@"cmd"] ?: @"";
    NSDictionary *args = payload[@"args"] ?: @{};

    dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
        @try {
            [self handleCommand:cmd args:args callId:callId];
        } @catch (NSException *ex) {
            NSLog(@"[Quantum Exception in %@]: %@", cmd, ex);
            [self sendErrorResponse:ex.reason callId:callId];
        }
    });
}

- (void)sendSuccessResponse:(id)result callId:(NSString *)callId {
    if (!callId) return;
    NSString *jsonStr = json_serialize_safe(result);
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *js = [NSString stringWithFormat:@"if (window.onNativeResponse) { window.onNativeResponse('%@', %@, null); }", callId, jsonStr];
        [self.webView evaluateJavaScript:js completionHandler:nil];
    });
}

- (void)sendErrorResponse:(NSString *)error callId:(NSString *)callId {
    if (!callId) return;
    NSString *errStr = json_serialize_safe(error ?: @"Unknown error");
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *js = [NSString stringWithFormat:@"if (window.onNativeResponse) { window.onNativeResponse('%@', null, %@); }", callId, errStr];
        [self.webView evaluateJavaScript:js completionHandler:nil];
    });
}

- (void)sendProgress:(double)pct ticker:(NSString *)ticker {
    NSString *tickerStr = json_serialize_safe(ticker ?: @"");
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *js = [NSString stringWithFormat:@"if (window.app && app.setIslandProgress) { app.setIslandProgress(%f, %@); }", pct, tickerStr];
        [self.webView evaluateJavaScript:js completionHandler:nil];
    });
}

- (void)sendLog:(NSString *)log {
    if (!log) return;
    NSString *logStr = json_serialize_safe(log);
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *js = [NSString stringWithFormat:@"if (window.app && app.appendLog) { app.appendLog(%@); }", logStr];
        [self.webView evaluateJavaScript:js completionHandler:nil];
    });
}

- (void)handleCommand:(NSString *)cmd args:(NSDictionary *)args callId:(NSString *)callId {
    if ([cmd isEqualToString:@"get_diagnostics"]) {
        char cpuName[256] = {0};
        size_t size = sizeof(cpuName);
        sysctlbyname("machdep.cpu.brand_string", &cpuName, &size, NULL, 0);

        char arch[64] = {0};
        size = sizeof(arch);
        sysctlbyname("hw.machine", &arch, &size, NULL, 0);

        NSOperatingSystemVersion osVer = [[NSProcessInfo processInfo] operatingSystemVersion];
        NSString *osStr = [NSString stringWithFormat:@"macOS %ld.%ld.%ld (Apple Silicon Universal)",
                           (long)osVer.majorVersion, (long)osVer.minorVersion, (long)osVer.patchVersion];

        BOOL hasBrew = [[NSFileManager defaultManager] fileExistsAtPath:@"/opt/homebrew/bin/brew"] ||
                       [[NSFileManager defaultManager] fileExistsAtPath:@"/usr/local/bin/brew"];

        NSArray *engines = @[
            @{ @"id": @"apple", @"name": @"macOS Software Update", @"available": @YES, @"version": @"Current OS" },
            @{ @"id": @"mac-apps", @"name": @"Installed Applications", @"available": @YES, @"version": @"Native" },
            @{ @"id": @"brew", @"name": @"Homebrew (Casks & Formulae)", @"available": @(hasBrew), @"version": hasBrew ? @"Ready" : @"Not Installed" }
        ];

        NSDictionary *diag = @{
            @"is_admin": @(geteuid() == 0),
            @"os_system": @"Darwin",
            @"os_display": @"macOS",
            @"os_info": osStr,
            @"arch": [NSString stringWithUTF8String:cpuName[0] ? cpuName : arch],
            @"active_engine": @"apple",
            @"engines": engines
        };
        [self sendSuccessResponse:diag callId:callId];

    } else if ([cmd isEqualToString:@"scan_cleanup"]) {
        NSString *home = NSHomeDirectory();
        NSArray *targets = @[
            @{ @"id": @"user_caches", @"name": @"User App & System Caches", @"tag": @"Safe to Clean", @"risk": @"low", @"desc": @"Temporary cached data from Safari, Chrome, Slack, Discord, and Electron apps.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Caches"]], @"icon": @"database" },
            @{ @"id": @"dev_caches", @"name": @"Developer & Tooling Caches", @"tag": @"Developer", @"risk": @"low", @"desc": @"Cached package archives from npm, Yarn, pip, and Cargo.", @"paths": @[[home stringByAppendingPathComponent:@".npm"], [home stringByAppendingPathComponent:@"Library/Caches/pip"], [home stringByAppendingPathComponent:@"Library/Caches/Yarn"]], @"icon": @"code" },
            @{ @"id": @"user_logs", @"name": @"Diagnostic & User Logs", @"tag": @"Telemetry", @"risk": @"low", @"desc": @"Historical crash reports and system log archives.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Logs"]], @"icon": @"file-text" },
            @{ @"id": @"trash", @"name": @"macOS Trash Bin", @"tag": @"Permanent", @"risk": @"medium", @"desc": @"Deleted files currently in the macOS Trash.", @"paths": @[[home stringByAppendingPathComponent:@".Trash"]], @"icon": @"trash-2" },
            @{ @"id": @"brew_cache", @"name": @"Homebrew Cache", @"tag": @"Package Cache", @"risk": @"low", @"desc": @"Stale downloaded bottle archives from Homebrew.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Caches/Homebrew"]], @"icon": @"archive" },
            @{ @"id": @"xcode_derived", @"name": @"Xcode DerivedData", @"tag": @"Apple SDK", @"risk": @"low", @"desc": @"Intermediate build artifacts and simulator caches.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Developer/Xcode/DerivedData"]], @"icon": @"cpu" }
        ];

        unsigned long long grandTotal = 0;
        unsigned long long grandFiles = 0;
        NSMutableArray *scannedCats = [NSMutableArray array];

        for (NSDictionary *t in targets) {
            unsigned long long catSize = 0;
            unsigned long long catFiles = 0;
            NSMutableArray *subItems = [NSMutableArray array];

            for (NSString *p in t[@"paths"]) {
                if ([[NSFileManager defaultManager] fileExistsAtPath:p]) {
                    NSArray *entries = [[NSFileManager defaultManager] contentsOfDirectoryAtPath:p error:nil];
                    for (NSString *entry in entries) {
                        NSString *full = [p stringByAppendingPathComponent:entry];
                        unsigned long long subFiles = 0;
                        unsigned long long subSz = calculate_dir_size(full, &subFiles);
                        catSize += subSz;
                        catFiles += subFiles;
                        if (subSz > 5 * 1024 * 1024) {
                            [subItems addObject:@{
                                @"name": entry,
                                @"size_bytes": @(subSz),
                                @"size_str": get_human_size(subSz),
                                @"file_count": @(subFiles)
                            }];
                        }
                    }
                }
            }
            [subItems sortUsingComparator:^NSComparisonResult(id obj1, id obj2) {
                return [obj2[@"size_bytes"] compare:obj1[@"size_bytes"]];
            }];

            grandTotal += catSize;
            grandFiles += catFiles;

            [scannedCats addObject:@{
                @"id": t[@"id"],
                @"name": t[@"name"],
                @"tag": t[@"tag"],
                @"risk": t[@"risk"],
                @"description": t[@"desc"],
                @"size_bytes": @(catSize),
                @"size_str": get_human_size(catSize),
                @"file_count": @(catFiles),
                @"sub_items": subItems,
                @"selected": @(catSize > 0 && [t[@"risk"] isEqualToString:@"low"]),
                @"svg_icon": t[@"icon"]
            }];
        }

        NSDictionary *attrs = [[NSFileManager defaultManager] attributesOfFileSystemForPath:home error:nil];
        unsigned long long freeBytes = [attrs[NSFileSystemFreeSize] unsignedLongLongValue];
        unsigned long long totalBytes = [attrs[NSFileSystemSize] unsignedLongLongValue];
        unsigned long long usedBytes = totalBytes > freeBytes ? (totalBytes - freeBytes) : 0;

        NSDictionary *res = @{
            @"total_reclaimable_bytes": @(grandTotal),
            @"total_reclaimable_str": get_human_size(grandTotal),
            @"total_files": @(grandFiles),
            @"disk_total_str": get_human_size(totalBytes),
            @"disk_used_str": get_human_size(usedBytes),
            @"disk_free_str": get_human_size(freeBytes),
            @"disk_used_percent": @(totalBytes > 0 ? (int)((usedBytes * 100) / totalBytes) : 0),
            @"categories": scannedCats
        };
        [self sendSuccessResponse:res callId:callId];

    } else if ([cmd isEqualToString:@"execute_cleanup"]) {
        NSArray *categories = args[@"categories"] ?: @[];
        NSString *home = NSHomeDirectory();
        NSDictionary *map = @{
            @"user_caches": @[[home stringByAppendingPathComponent:@"Library/Caches"]],
            @"dev_caches": @[[home stringByAppendingPathComponent:@".npm"], [home stringByAppendingPathComponent:@"Library/Caches/pip"], [home stringByAppendingPathComponent:@"Library/Caches/Yarn"]],
            @"user_logs": @[[home stringByAppendingPathComponent:@"Library/Logs"]],
            @"trash": @[[home stringByAppendingPathComponent:@".Trash"]],
            @"brew_cache": @[[home stringByAppendingPathComponent:@"Library/Caches/Homebrew"]],
            @"xcode_derived": @[[home stringByAppendingPathComponent:@"Library/Developer/Xcode/DerivedData"]]
        };

        unsigned long long freed = 0;
        NSFileManager *fm = [NSFileManager defaultManager];
        for (NSString *cat in categories) {
            NSArray *paths = map[cat];
            for (NSString *p in paths) {
                if ([fm fileExistsAtPath:p]) {
                    NSArray *items = [fm contentsOfDirectoryAtPath:p error:nil];
                    for (NSString *item in items) {
                        NSString *subPath = [p stringByAppendingPathComponent:item];
                        unsigned long long sz = calculate_dir_size(subPath, nil);
                        if ([fm removeItemAtPath:subPath error:nil]) {
                            freed += sz;
                        }
                    }
                }
            }
        }

        NSDictionary *resp = @{
            @"success": @YES,
            @"total_freed_bytes": @(freed),
            @"total_freed_str": get_human_size(freed)
        };
        [self sendSuccessResponse:resp callId:callId];

    } else if ([cmd isEqualToString:@"scan_updates"]) {
        [self sendLog:@"Querying official Apple software updates via /usr/sbin/softwareupdate..."];
        NSTask *task = [[NSTask alloc] init];
        task.launchPath = @"/usr/sbin/softwareupdate";
        task.arguments = @[@"-l"];
        NSPipe *pipe = [NSPipe pipe];
        task.standardOutput = pipe;
        task.standardError = pipe;
        [task launch];

        NSData *data = [[pipe fileHandleForReading] readDataToEndOfFile];
        [task waitUntilExit];
        NSString *output = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"";

        NSMutableArray *pkgs = [NSMutableArray array];
        for (NSString *line in [output componentsSeparatedByString:@"\n"]) {
            NSString *trimmed = [line stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
            if ([trimmed hasPrefix:@"* Label:"]) {
                NSString *label = [trimmed substringFromIndex:8];
                [pkgs addObject:@{
                    @"id": [label stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]],
                    @"name": [label stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]],
                    @"installed_version": @"Current OS",
                    @"available_version": @"Apple Recommended",
                    @"source": @"apple-system"
                }];
            }
        }
        [self sendSuccessResponse:pkgs callId:callId];

    } else if ([cmd isEqualToString:@"get_installed"]) {
        NSMutableArray *apps = [NSMutableArray array];
        NSArray *roots = @[@"/Applications", [NSHomeDirectory() stringByAppendingPathComponent:@"Applications"]];
        for (NSString *dir in roots) {
            NSArray *items = [[NSFileManager defaultManager] contentsOfDirectoryAtPath:dir error:nil];
            for (NSString *item in items) {
                if ([item hasSuffix:@".app"]) {
                    NSString *plist = [dir stringByAppendingPathComponent:[NSString stringWithFormat:@"%@/Contents/Info.plist", item]];
                    NSDictionary *dict = [NSDictionary dictionaryWithContentsOfFile:plist];
                    NSString *ver = dict[@"CFBundleShortVersionString"] ?: dict[@"CFBundleVersion"] ?: @"1.0";
                    NSString *name = dict[@"CFBundleDisplayName"] ?: dict[@"CFBundleName"] ?: [item stringByDeletingPathExtension];
                    NSString *bundleId = dict[@"CFBundleIdentifier"] ?: [item stringByDeletingPathExtension];
                    [apps addObject:@{
                        @"id": bundleId,
                        @"name": name,
                        @"installed_version": ver,
                        @"available_version": @"",
                        @"source": @"mac-app"
                    }];
                }
            }
        }
        [self sendSuccessResponse:apps callId:callId];

    } else if ([cmd isEqualToString:@"check_self_update"]) {
        NSString *currentVer = [[NSBundle mainBundle] infoDictionary][@"CFBundleShortVersionString"] ?: @"2.0.0";
        NSURL *url = [NSURL URLWithString:@"https://api.github.com/repos/merchanthasan2/AudoChocolatey/releases/latest"];
        NSMutableURLRequest *req = [NSMutableURLRequest requestWithURL:url];
        [req setValue:[NSString stringWithFormat:@"QuantumUpdater/%@ (macOS; Universal)", currentVer] forHTTPHeaderField:@"User-Agent"];
        [req setValue:@"application/vnd.github.v3+json" forHTTPHeaderField:@"Accept"];
        req.timeoutInterval = 12.0;

        NSURLSessionDataTask *task = [[NSURLSession sharedSession] dataTaskWithRequest:req completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
            if (error || !data) {
                [self sendSuccessResponse:@{
                    @"has_update": @NO,
                    @"current_version": currentVer,
                    @"error": error ? error.localizedDescription : @"No response from server"
                } callId:callId];
                return;
            }

            NSDictionary *json = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
            if (![json isKindOfClass:[NSDictionary class]]) {
                [self sendSuccessResponse:@{ @"has_update": @NO, @"current_version": currentVer } callId:callId];
                return;
            }

            NSString *tagName = json[@"tag_name"] ?: @"";
            NSString *latestVer = [tagName stringByTrimmingCharactersInSet:[NSCharacterSet characterSetWithCharactersInString:@"vV "]];
            BOOL hasUpdate = (compare_semver(currentVer, latestVer) == NSOrderedAscending);

            NSArray *assets = json[@"assets"] ?: @[];
            NSDictionary *matchedAsset = nil;
            // 1. Prefer macOS update zip for direct in-place extraction
            for (NSDictionary *a in assets) {
                NSString *name = [a[@"name"] lowercaseString];
                if ([name containsString:@"macos"] && [name hasSuffix:@".zip"]) {
                    matchedAsset = a;
                    break;
                }
            }
            if (!matchedAsset) {
                for (NSDictionary *a in assets) {
                    NSString *name = [a[@"name"] lowercaseString];
                    if ([name hasSuffix:@".zip"]) {
                        matchedAsset = a;
                        break;
                    }
                }
            }
            // 2. Fallback to DMG
            if (!matchedAsset) {
                for (NSDictionary *a in assets) {
                    NSString *name = [a[@"name"] lowercaseString];
                    if ([name hasSuffix:@".dmg"]) {
                        matchedAsset = a;
                        break;
                    }
                }
            }

            unsigned long long sz = [matchedAsset[@"size"] unsignedLongLongValue];
            NSDictionary *resp = @{
                @"has_update": @(hasUpdate),
                @"current_version": currentVer,
                @"latest_version": latestVer,
                @"tag_name": tagName,
                @"release_name": json[@"name"] ?: [NSString stringWithFormat:@"Quantum Updater %@", tagName],
                @"release_notes": json[@"body"] ?: @"Performance and stability improvements.",
                @"published_at": json[@"published_at"] ?: @"",
                @"download_url": matchedAsset[@"browser_download_url"] ?: @"",
                @"asset_name": matchedAsset[@"name"] ?: @"",
                @"asset_size": @(sz),
                @"asset_size_str": get_human_size(sz)
            };
            [self sendSuccessResponse:resp callId:callId];
        }];
        [task resume];

    } else if ([cmd isEqualToString:@"apply_self_update"]) {
        NSString *downloadUrl = args[@"downloadUrl"] ?: @"";
        if (downloadUrl.length == 0) {
            [self sendErrorResponse:@"No download URL provided" callId:callId];
            return;
        }

        [self sendLog:@"[Quantum Auto-Updater]: Connecting to GitHub release distribution..."];
        [self sendProgress:0.1 ticker:@"Connecting to release server..."];

        NSURL *url = [NSURL URLWithString:downloadUrl];
        NSURLRequest *req = [NSURLRequest requestWithURL:url];

        NSURLSessionDownloadTask *dlTask = [[NSURLSession sharedSession] downloadTaskWithRequest:req completionHandler:^(NSURL *location, NSURLResponse *response, NSError *error) {
            if (error || !location) {
                [self sendLog:[NSString stringWithFormat:@"[Quantum Auto-Updater Error]: Download failed: %@", error.localizedDescription]];
                [self sendErrorResponse:error.localizedDescription callId:callId];
                return;
            }

            [self sendLog:@"[Quantum Auto-Updater]: Payload downloaded successfully. Applying in-place update..."];
            [self sendProgress:0.95 ticker:@"Applying in-place update..."];

            NSString *tmpPath = [NSTemporaryDirectory() stringByAppendingPathComponent:[NSString stringWithFormat:@"quantum_update_%u", arc4random()]];
            [[NSFileManager defaultManager] moveItemAtURL:location toURL:[NSURL fileURLWithPath:tmpPath] error:nil];

            NSString *destBundle = [[NSBundle mainBundle] bundlePath];
            if (![destBundle hasSuffix:@".app"]) {
                destBundle = @"/Applications/Quantum Updater.app";
            }

            NSString *nameLower = [downloadUrl lowercaseString];
            if ([nameLower containsString:@".zip"]) {
                NSString *extractDir = [NSTemporaryDirectory() stringByAppendingPathComponent:[NSString stringWithFormat:@"quantum_extract_%u", arc4random()]];
                [[NSFileManager defaultManager] createDirectoryAtPath:extractDir withIntermediateDirectories:YES attributes:nil error:nil];

                NSTask *unzip = [[NSTask alloc] init];
                unzip.launchPath = @"/usr/bin/ditto";
                unzip.arguments = @[@"-xk", tmpPath, extractDir];
                [unzip launch];
                [unzip waitUntilExit];

                NSString *stagedApp = [extractDir stringByAppendingPathComponent:@"Quantum Updater.app"];
                if (![[NSFileManager defaultManager] fileExistsAtPath:stagedApp]) {
                    NSArray *sub = [[NSFileManager defaultManager] contentsOfDirectoryAtPath:extractDir error:nil];
                    for (NSString *s in sub) {
                        if ([s hasSuffix:@".app"]) {
                            stagedApp = [extractDir stringByAppendingPathComponent:s];
                            break;
                        }
                    }
                }

                NSString *swapScript = [NSString stringWithFormat:
                    @"sleep 0.8; rm -rf \"%@\"; /usr/bin/ditto \"%@\" \"%@\"; /usr/bin/open \"%@\"",
                    destBundle, stagedApp, destBundle, destBundle];

                NSTask *rel = [[NSTask alloc] init];
                rel.launchPath = @"/bin/sh";
                rel.arguments = @[@"-c", swapScript];
                [rel launch];

            } else {
                // DMG fallback
                NSString *swapScript = [NSString stringWithFormat:
                    @"sleep 0.8; MOUNT=$(hdiutil attach -nobrowse -readonly \"%@\" | grep /Volumes | awk '{print $3}'); "
                    "rm -rf \"%@\"; /usr/bin/ditto \"$MOUNT/Quantum Updater.app\" \"%@\"; hdiutil detach \"$MOUNT\" -force; /usr/bin/open \"%@\"",
                    tmpPath, destBundle, destBundle, destBundle];

                NSTask *rel = [[NSTask alloc] init];
                rel.launchPath = @"/bin/sh";
                rel.arguments = @[@"-c", swapScript];
                [rel launch];
            }

            [self sendLog:@"[Quantum Auto-Updater]: Restarting Quantum Updater with latest version..."];
            [self sendSuccessResponse:@{ @"success": @YES } callId:callId];

            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.6 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                [NSApp terminate:nil];
            });
        }];
        [dlTask resume];

    } else {
        [self sendSuccessResponse:@{} callId:callId];
    }
}

@end

@interface AppDelegate : NSObject <NSApplicationDelegate>
@end

@implementation AppDelegate
- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication *)sender {
    return YES;
}
@end

int main(int argc, const char * argv[]) {
    @autoreleasepool {
        NSApplication *app = [NSApplication sharedApplication];
        AppDelegate *delegate = [[AppDelegate alloc] init];
        app.delegate = delegate;
        [app setActivationPolicy:NSApplicationActivationPolicyRegular];

        NSRect frame = NSMakeRect(0, 0, 1200, 780);
        NSWindow *window = [[NSWindow alloc] initWithContentRect:frame
                                                       styleMask:(NSWindowStyleMaskTitled |
                                                                 NSWindowStyleMaskClosable |
                                                                 NSWindowStyleMaskMiniaturizable |
                                                                 NSWindowStyleMaskResizable |
                                                                 NSWindowStyleMaskFullSizeContentView)
                                                         backing:NSBackingStoreBuffered
                                                           defer:NO];
        window.title = @"Quantum Updater • System & Package Manager";
        window.titleVisibility = NSWindowTitleHidden;
        window.titlebarAppearsTransparent = YES;
        [window center];

        // Frosted Glass Effect
        NSVisualEffectView *vfx = [[NSVisualEffectView alloc] initWithFrame:window.contentView.bounds];
        vfx.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        vfx.material = NSVisualEffectMaterialUnderWindowBackground;
        vfx.blendingMode = NSVisualEffectBlendingModeBehindWindow;
        vfx.state = NSVisualEffectStateActive;
        [window.contentView addSubview:vfx];

        // WebKit Configuration
        WKWebViewConfiguration *config = [[WKWebViewConfiguration alloc] init];
        QuantumBridge *bridge = [[QuantumBridge alloc] init];
        [config.userContentController addScriptMessageHandler:bridge name:@"quantum"];

        WKWebView *webView = [[WKWebView alloc] initWithFrame:window.contentView.bounds configuration:config];
        webView.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        webView.navigationDelegate = bridge;
        bridge.webView = webView;
        [webView setValue:@NO forKey:@"drawsBackground"];

        [window.contentView addSubview:webView];
        [window makeKeyAndOrderFront:nil];
        [app activateIgnoringOtherApps:YES];

        // Resolve web/index.html
        NSString *webPath = [[[NSBundle mainBundle] resourcePath] stringByAppendingPathComponent:@"web/index.html"];
        if (![[NSFileManager defaultManager] fileExistsAtPath:webPath]) {
            // Local dev fallback
            webPath = [[[NSFileManager defaultManager] currentDirectoryPath] stringByAppendingPathComponent:@"web/index.html"];
        }
        NSURL *url = [NSURL fileURLWithPath:webPath];
        [webView loadFileURL:url allowingReadAccessToURL:[url URLByDeletingLastPathComponent]];

        [app run];
    }
    return 0;
}
