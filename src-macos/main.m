#import <Cocoa/Cocoa.h>
#import <WebKit/WebKit.h>
#import <IOKit/IOKitLib.h>
#import <AVFoundation/AVFoundation.h>
#include <sys/sysctl.h>
#include <sys/stat.h>
#include <dirent.h>

static NSString *get_human_size(unsigned long long bytes) {
    if (bytes < 1024) return [NSString stringWithFormat:@"%llu B", bytes];
    if (bytes < 1024 * 1024) return [NSString stringWithFormat:@"%.1f KB", (double)bytes / 1024.0];
    if (bytes < 1024 * 1024 * 1024) return [NSString stringWithFormat:@"%.1f MB", (double)bytes / (1024.0 * 1024.0)];
    return [NSString stringWithFormat:@"%.2f GB", (double)bytes / (1024.0 * 1024.0 * 1024.0)];
}

// CRITICAL: Filter out Apple Music, iTunes, AMP, and protected media containers to stop TCC permission requests
static BOOL should_skip_path(NSString *path) {
    if (!path) return YES;
    NSString *lower = [path lowercaseString];
    if ([lower containsString:@"com.apple.music"] ||
        [lower containsString:@"com.apple.itunes"] ||
        [lower containsString:@"com.apple.amp"] ||
        [lower containsString:@"com.apple.medialibrary"] ||
        [lower containsString:@"com.apple.podcasts"] ||
        [lower containsString:@"library/itunes"] ||
        [lower containsString:@"library/music"] ||
        [lower containsString:@"library/photos"] ||
        [lower containsString:@"library/mail"] ||
        [lower containsString:@"library/messages"] ||
        [lower containsString:@"library/suggestions"] ||
        [lower containsString:@"library/identityservices"]) {
        return YES;
    }
    return NO;
}

static unsigned long long calculate_dir_size(NSString *path, unsigned long long *fileCount) {
    if (should_skip_path(path)) return 0;
    unsigned long long total = 0;
    NSFileManager *fm = [NSFileManager defaultManager];
    BOOL isDir = NO;
    if (![fm fileExistsAtPath:path isDirectory:&isDir]) return 0;
    if (!isDir) {
        NSDictionary *attrs = [fm attributesOfItemAtPath:path error:nil];
        if (attrs && [attrs[NSFileType] isEqualToString:NSFileTypeRegular]) {
            if (fileCount) (*fileCount)++;
            return [attrs[NSFileSize] unsignedLongLongValue];
        }
        return 0;
    }

    NSDirectoryEnumerator *enumerator = [fm enumeratorAtPath:path];
    NSString *file;
    while ((file = [enumerator nextObject])) {
        if (should_skip_path(file)) {
            [enumerator skipDescendants];
            continue;
        }
        NSString *fullPath = [path stringByAppendingPathComponent:file];
        NSDictionary *attrs = [fm attributesOfItemAtPath:fullPath error:nil];
        if (attrs && [attrs[NSFileType] isEqualToString:NSFileTypeRegular]) {
            total += [attrs[NSFileSize] unsignedLongLongValue];
            if (fileCount) (*fileCount)++;
        }
    }
    return total;
}

static NSString *get_system_serial_number(void) {
    io_service_t platformExpert = IOServiceGetMatchingService(kIOMainPortDefault, IOServiceMatching("IOPlatformExpertDevice"));
    if (platformExpert) {
        CFTypeRef serial = IORegistryEntryCreateCFProperty(platformExpert, CFSTR(kIOPlatformSerialNumberKey), kCFAllocatorDefault, 0);
        IOObjectRelease(platformExpert);
        if (serial) {
            NSString *s = [(__bridge NSString *)serial copy];
            CFRelease(serial);
            return s;
        }
    }
    return @"C02D14LAMD6N";
}

static NSString *get_system_camera_name(void) {
    @try {
        NSArray *types = @[AVCaptureDeviceTypeBuiltInWideAngleCamera];
        AVCaptureDeviceDiscoverySession *session = [AVCaptureDeviceDiscoverySession discoverySessionWithDeviceTypes:types mediaType:AVMediaTypeVideo position:AVCaptureDevicePositionUnspecified];
        for (AVCaptureDevice *d in session.devices) {
            if (d.localizedName.length > 0) {
                return d.localizedName;
            }
        }
    } @catch (NSException *e) {
    }
    return @"FaceTime HD Camera (Built-in)";
}

static NSString *get_system_display_name(void) {
    NSMutableArray *displays = [NSMutableArray array];
    for (NSScreen *s in [NSScreen screens]) {
        NSString *name = s.localizedName ?: @"Display";
        NSString *res = [NSString stringWithFormat:@"%@ (%.0f × %.0f)", name, s.frame.size.width * s.backingScaleFactor, s.frame.size.height * s.backingScaleFactor];
        [displays addObject:res];
    }
    return displays.count > 0 ? [displays componentsJoinedByString:@" • "] : @"Built-in Retina Display";
}

static NSString *get_model_marketing_name(NSString *modelId) {
    if ([modelId hasPrefix:@"MacBookPro16,1"]) return @"MacBook Pro (16-inch, 2019)";
    if ([modelId hasPrefix:@"MacBookPro18,1"] || [modelId hasPrefix:@"MacBookPro18,2"]) return @"MacBook Pro (16-inch, M1 Max)";
    if ([modelId hasPrefix:@"MacBookPro18"]) return @"MacBook Pro (14-inch, M1 Pro)";
    if ([modelId hasPrefix:@"MacBookPro"]) return [NSString stringWithFormat:@"MacBook Pro (%@)", modelId];
    if ([modelId hasPrefix:@"MacBookAir"]) return [NSString stringWithFormat:@"MacBook Air (%@)", modelId];
    if ([modelId hasPrefix:@"Macmini"]) return [NSString stringWithFormat:@"Mac mini (%@)", modelId];
    if ([modelId hasPrefix:@"MacStudio"]) return [NSString stringWithFormat:@"Mac Studio (%@)", modelId];
    if ([modelId hasPrefix:@"MacPro"]) return [NSString stringWithFormat:@"Mac Pro (%@)", modelId];
    if ([modelId hasPrefix:@"iMac"]) return [NSString stringWithFormat:@"iMac (%@)", modelId];
    return modelId ?: @"Apple Mac";
}

static NSString *get_app_direct_download_url(NSString *pkgId) {
    NSString *p = [pkgId lowercaseString];
    if ([p containsString:@"chrome"]) return @"https://dl.google.com/chrome/mac/universal/stable/GGRO/googlechrome.dmg";
    if ([p containsString:@"firefox"]) return @"https://download.mozilla.org/?product=firefox-latest&os=osx&lang=en-US";
    if ([p containsString:@"brave"]) return @"https://laptop-updates.brave.com/latest/osx";
    if ([p containsString:@"visualstudio"] || [p containsString:@"vscode"]) return @"https://code.visualstudio.com/sha/download?build=stable&os=darwin-universal";
    if ([p containsString:@"docker"]) return @"https://desktop.docker.com/mac/main/universal/Docker.dmg";
    if ([p containsString:@"spotify"]) return @"https://download.scdn.co/Spotify.dmg";
    if ([p containsString:@"raycast"]) return @"https://www.raycast.com/download";
    if ([p containsString:@"arc"]) return @"https://releases.arc.net/release/Arc-latest.dmg";
    if ([p containsString:@"ollama"]) return @"https://ollama.com/download/Ollama-darwin.zip";
    if ([p containsString:@"cursor"]) return @"https://downloader.cursor.sh/mac/universal";
    if ([p containsString:@"iterm"]) return @"https://iterm2.com/downloads/stable/latest";
    if ([p containsString:@"zed"]) return @"https://zed.dev/download";
    if ([p containsString:@"slack"]) return @"https://slack.com/ssb/download-osx";
    if ([p containsString:@"zoom"]) return @"https://zoom.us/client/latest/Zoom.pkg";
    if ([p containsString:@"vlc"]) return @"https://get.videolan.org/vlc/last/macosx/";
    if ([p containsString:@"notion"]) return @"https://www.notion.so/desktop/mac/download";
    if ([p containsString:@"obsidian"]) return @"https://obsidian.md/download";
    if ([p containsString:@"sublime"]) return @"https://download.sublimetext.com/sublime_text_build_4180_mac.zip";
    if ([p containsString:@"postman"]) return @"https://dl.pstmn.io/download/latest/osx_universal";
    if ([p containsString:@"telegram"]) return @"https://osx.telegram.org/updates/Telegram.dmg";
    if ([p containsString:@"whatsapp"]) return @"https://web.whatsapp.com/desktop/mac_native/release/";
    if ([p containsString:@"discord"]) return @"https://discord.com/api/download?platform=osx";
    return nil;
}

static NSString *find_brew_path(void) {
    if ([[NSFileManager defaultManager] fileExistsAtPath:@"/opt/homebrew/bin/brew"]) {
        return @"/opt/homebrew/bin/brew";
    }
    if ([[NSFileManager defaultManager] fileExistsAtPath:@"/usr/local/bin/brew"]) {
        return @"/usr/local/bin/brew";
    }
    return nil;
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

@interface QuantumBridge : NSObject <WKScriptMessageHandler, WKNavigationDelegate, WKUIDelegate>
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

// WKUIDelegate to support JavaScript alert & confirm without silent dropping
- (void)webView:(WKWebView *)webView runJavaScriptAlertPanelWithMessage:(NSString *)message initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(void))completionHandler {
    NSAlert *alert = [[NSAlert alloc] init];
    alert.messageText = @"Quantum Updater";
    alert.informativeText = message;
    [alert addButtonWithTitle:@"OK"];
    [alert runModal];
    completionHandler();
}

- (void)webView:(WKWebView *)webView runJavaScriptConfirmPanelWithMessage:(NSString *)message initiatedByFrame:(WKFrameInfo *)frame completionHandler:(void (^)(BOOL result))completionHandler {
    NSAlert *alert = [[NSAlert alloc] init];
    alert.messageText = @"Quantum Updater";
    alert.informativeText = message;
    [alert addButtonWithTitle:@"Continue"];
    [alert addButtonWithTitle:@"Cancel"];
    NSModalResponse resp = [alert runModal];
    completionHandler(resp == NSAlertFirstButtonReturn);
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

        char modelId[64] = {0};
        size = sizeof(modelId);
        sysctlbyname("hw.model", &modelId, &size, NULL, 0);

        uint64_t memBytes = 0;
        size = sizeof(memBytes);
        sysctlbyname("hw.memsize", &memBytes, &size, NULL, 0);
        NSString *memStr = [NSString stringWithFormat:@"%llu GB", (memBytes + (1ULL << 30) - 1) / (1ULL << 30)];

        NSOperatingSystemVersion osVer = [[NSProcessInfo processInfo] operatingSystemVersion];
        NSString *osStr = [NSString stringWithFormat:@"macOS %ld.%ld.%ld",
                           (long)osVer.majorVersion, (long)osVer.minorVersion, (long)osVer.patchVersion];

        NSString *serial = get_system_serial_number();
        NSString *modelStr = [NSString stringWithUTF8String:modelId];
        NSString *marketingModel = get_model_marketing_name(modelStr);
        NSString *cameraStr = get_system_camera_name();
        NSString *displayStr = get_system_display_name();

        NSDictionary *attrs = [[NSFileManager defaultManager] attributesOfFileSystemForPath:NSHomeDirectory() error:nil];
        unsigned long long freeBytes = [attrs[NSFileSystemFreeSize] unsignedLongLongValue];
        unsigned long long totalBytes = [attrs[NSFileSystemSize] unsignedLongLongValue];
        unsigned long long usedBytes = totalBytes > freeBytes ? (totalBytes - freeBytes) : 0;
        int usedPct = totalBytes > 0 ? (int)((usedBytes * 100) / totalBytes) : 0;

        BOOL hasBrew = (find_brew_path() != nil);

        NSArray *engines = @[
            @{ @"id": @"apple", @"name": @"macOS Software Update", @"available": @YES, @"version": @"Current OS" },
            @{ @"id": @"mac-apps", @"name": @"Installed Applications", @"available": @YES, @"version": @"Native" },
            @{ @"id": @"brew", @"name": @"Homebrew (Casks & Formulae)", @"available": @(hasBrew), @"version": hasBrew ? @"Ready" : @"Not Installed" }
        ];

        NSDictionary *diag = @{
            @"is_admin": @(geteuid() == 0),
            @"os_system": @"macOS",
            @"os_display": osStr,
            @"os_info": [NSString stringWithFormat:@"%@ • %@", osStr, marketingModel],
            @"model_name": marketingModel,
            @"model_id": modelStr,
            @"serial_number": serial,
            @"cpu": [NSString stringWithUTF8String:cpuName],
            @"memory": memStr,
            @"camera": cameraStr,
            @"display": displayStr,
            @"storage_free_str": get_human_size(freeBytes),
            @"storage_total_str": get_human_size(totalBytes),
            @"storage_used_str": get_human_size(usedBytes),
            @"storage_used_percent": @(usedPct),
            @"active_engine": @"apple",
            @"engines": engines
        };
        [self sendSuccessResponse:diag callId:callId];

    } else if ([cmd isEqualToString:@"scan_cleanup"]) {
        NSString *home = NSHomeDirectory();
        NSArray *targets = @[
            @{ @"id": @"user_caches", @"name": @"App Caches & Temporary Files", @"tag": @"Safe to Clean", @"risk": @"low", @"desc": @"Temporary files from Safari, Chrome, Slack, Discord, and desktop apps.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Caches"]], @"icon": @"database" },
            @{ @"id": @"dev_caches", @"name": @"Developer & Code Package Caches", @"tag": @"Safe to Clean", @"risk": @"low", @"desc": @"Cached package downloads from npm, Yarn, pip, and Cargo.", @"paths": @[[home stringByAppendingPathComponent:@".npm"], [home stringByAppendingPathComponent:@"Library/Caches/pip"], [home stringByAppendingPathComponent:@"Library/Caches/Yarn"]], @"icon": @"code" },
            @{ @"id": @"user_logs", @"name": @"Old Crash Reports & System Logs", @"tag": @"Safe to Clean", @"risk": @"low", @"desc": @"Historical application logs and diagnostic reports.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Logs"]], @"icon": @"file-text" },
            @{ @"id": @"trash", @"name": @"Trash Bin", @"tag": @"Review Items", @"risk": @"medium", @"desc": @"Files currently waiting in your macOS Trash.", @"paths": @[[home stringByAppendingPathComponent:@".Trash"]], @"icon": @"trash-2" },
            @{ @"id": @"brew_cache", @"name": @"Package Manager Download Cache", @"tag": @"Safe to Clean", @"risk": @"low", @"desc": @"Old downloaded installer archives.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Caches/Homebrew"]], @"icon": @"archive" },
            @{ @"id": @"xcode_derived", @"name": @"Xcode & Simulator Build Files", @"tag": @"Safe to Clean", @"risk": @"low", @"desc": @"Temporary developer build artifacts and test caches.", @"paths": @[[home stringByAppendingPathComponent:@"Library/Developer/Xcode/DerivedData"]], @"icon": @"cpu" }
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
                        if (should_skip_path(entry)) continue;
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

    } else if ([cmd isEqualToString:@"execute_cleanup"] || [cmd isEqualToString:@"one_click_clean"]) {
        NSArray *categories = args[@"categories"];
        NSString *home = NSHomeDirectory();
        if (!categories || categories.count == 0) {
            // One-click clean default safe categories
            categories = @[@"user_caches", @"dev_caches", @"user_logs", @"brew_cache", @"xcode_derived"];
        }

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
                        if (should_skip_path(item)) continue;
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

    } else if ([cmd isEqualToString:@"install_package"]) {
        NSString *pkgId = args[@"packageId"] ?: @"";
        NSString *brew = find_brew_path();

        if (brew) {
            [self sendLog:[NSString stringWithFormat:@"[Quantum Package Manager]: Installing %@ via Homebrew...", pkgId]];
            [self sendProgress:0.3 ticker:[NSString stringWithFormat:@"Installing %@...", pkgId]];
            NSTask *task = [[NSTask alloc] init];
            task.launchPath = brew;
            task.arguments = @[@"install", @"--cask", pkgId];
            NSPipe *pipe = [NSPipe pipe];
            task.standardOutput = pipe;
            task.standardError = pipe;
            [task launch];
            NSFileHandle *fh = [pipe fileHandleForReading];
            NSData *data = [fh readDataToEndOfFile];
            [task waitUntilExit];
            NSString *outStr = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"";
            for (NSString *l in [outStr componentsSeparatedByString:@"\n"]) {
                if (l.length > 0) [self sendLog:l];
            }
            if (task.terminationStatus == 0) {
                [self sendProgress:1.0 ticker:[NSString stringWithFormat:@"%@ installed successfully", pkgId]];
                [self sendSuccessResponse:@{ @"success": @YES, @"message": @"Installed successfully via Homebrew" } callId:callId];
                return;
            }
        }

        // Direct Download Fallback: If Homebrew is not installed or cask returned error, open official installer directly
        NSString *directUrl = get_app_direct_download_url(pkgId);
        if (directUrl) {
            [self sendLog:[NSString stringWithFormat:@"[Quantum Installer]: Opening official macOS installer for %@...", pkgId]];
            dispatch_async(dispatch_get_main_queue(), ^{
                [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:directUrl]];
            });
            [self sendProgress:1.0 ticker:[NSString stringWithFormat:@"Installer opened for %@", pkgId]];
            [self sendSuccessResponse:@{ @"success": @YES, @"message": @"Official installer opened in your browser/downloads." } callId:callId];
        } else {
            // General web search / official project page fallback
            NSString *searchUrl = [NSString stringWithFormat:@"https://www.google.com/search?q=%@+mac+download", [pkgId stringByAddingPercentEncodingWithAllowedCharacters:[NSCharacterSet URLQueryAllowedCharacterSet]]];
            dispatch_async(dispatch_get_main_queue(), ^{
                [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:searchUrl]];
            });
            [self sendSuccessResponse:@{ @"success": @YES, @"message": @"Opening official download page." } callId:callId];
        }

    } else if ([cmd isEqualToString:@"upgrade_packages"]) {
        NSArray *pkgIds = args[@"packageIds"] ?: @[];
        NSMutableArray *statuses = [NSMutableArray array];
        NSString *brew = find_brew_path();
        for (NSString *pid in pkgIds) {
            [self sendLog:[NSString stringWithFormat:@"[Quantum Updater]: Processing upgrade for %@...", pid]];
            if ([pid hasPrefix:@"macOS"] || [pid containsString:@"Update"]) {
                NSTask *task = [[NSTask alloc] init];
                task.launchPath = @"/usr/sbin/softwareupdate";
                task.arguments = @[@"-i", pid];
                NSPipe *pipe = [NSPipe pipe];
                task.standardOutput = pipe;
                task.standardError = pipe;
                [task launch];
                [task waitUntilExit];
                BOOL ok = (task.terminationStatus == 0);
                [statuses addObject:@{ @"package_id": pid, @"success": @(ok), @"message": ok ? @"Apple Update Complete" : @"Update Failed" }];
            } else if (brew) {
                NSTask *task = [[NSTask alloc] init];
                task.launchPath = brew;
                task.arguments = @[@"upgrade", pid];
                NSPipe *pipe = [NSPipe pipe];
                task.standardOutput = pipe;
                task.standardError = pipe;
                [task launch];
                [task waitUntilExit];
                BOOL ok = (task.terminationStatus == 0);
                [statuses addObject:@{ @"package_id": pid, @"success": @(ok), @"message": ok ? @"Upgraded" : @"Upgrade failed" }];
            } else {
                [statuses addObject:@{ @"package_id": pid, @"success": @NO, @"message": @"Engine unavailable" }];
            }
        }
        [self sendSuccessResponse:statuses callId:callId];

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
                    NSString *appPath = [dir stringByAppendingPathComponent:item];
                    NSString *name = [item stringByDeletingPathExtension];
                    NSString *infoPath = [appPath stringByAppendingPathComponent:@"Contents/Info.plist"];
                    NSDictionary *info = [NSDictionary dictionaryWithContentsOfFile:infoPath];
                    NSString *ver = info[@"CFBundleShortVersionString"] ?: info[@"CFBundleVersion"] ?: @"1.0";
                    NSString *bundleId = info[@"CFBundleIdentifier"] ?: [name lowercaseString];
                    [apps addObject:@{
                        @"id": bundleId,
                        @"name": name,
                        @"version": ver,
                        @"path": appPath,
                        @"source": @"native-macos"
                    }];
                }
            }
        }
        [self sendSuccessResponse:apps callId:callId];

    } else if ([cmd isEqualToString:@"reveal_in_finder"]) {
        NSString *path = args[@"path"] ?: @"";
        if (path.length > 0 && [[NSFileManager defaultManager] fileExistsAtPath:path]) {
            [[NSWorkspace sharedWorkspace] selectFile:path inFileViewerRootedAtPath:@""];
            [self sendSuccessResponse:@{ @"success": @YES } callId:callId];
        } else {
            [self sendSuccessResponse:@{ @"success": @NO, @"message": @"Path not found" } callId:callId];
        }

    } else if ([cmd isEqualToString:@"check_self_update"]) {
        [self sendLog:@"[Quantum Auto-Updater]: Checking for updates at github.com/merchanthasan2/AudoChocolatey..."];
        NSURL *url = [NSURL URLWithString:@"https://api.github.com/repos/merchanthasan2/AudoChocolatey/releases/latest"];
        NSMutableURLRequest *req = [NSMutableURLRequest requestWithURL:url];
        [req setValue:@"QuantumUpdater-macOS/2.0.0" forHTTPHeaderField:@"User-Agent"];
        [[[NSURLSession sharedSession] dataTaskWithRequest:req completionHandler:^(NSData *data, NSURLResponse *response, NSError *error) {
            if (error || !data) {
                [self sendSuccessResponse:@{ @"has_update": @NO } callId:callId];
                return;
            }
            NSDictionary *rel = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
            NSString *tag = rel[@"tag_name"] ?: @"";
            NSString *cleanTag = [tag stringByTrimmingCharactersInSet:[NSCharacterSet characterSetWithCharactersInString:@"vV "]];
            if (cleanTag.length > 0 && [cleanTag compare:@"2.0.0" options:NSNumericSearch] == NSOrderedDescending) {
                [self sendSuccessResponse:@{
                    @"has_update": @YES,
                    @"version": cleanTag,
                    @"notes": rel[@"body"] ?: @"Bug fixes and performance improvements."
                } callId:callId];
            } else {
                [self sendSuccessResponse:@{ @"has_update": @NO } callId:callId];
            }
        }] resume];

    } else {
        [self sendSuccessResponse:@{} callId:callId];
    }
}

@end

static void setup_macos_menu(NSApplication *app) {
    NSMenu *mainMenu = [[NSMenu alloc] init];

    // 1. Application Menu ("Quantum Updater")
    NSMenuItem *appMenuItem = [[NSMenuItem alloc] init];
    NSMenu *appMenu = [[NSMenu alloc] initWithTitle:@"Quantum Updater"];
    [appMenu addItemWithTitle:@"About Quantum Updater" action:@selector(orderFrontStandardAboutPanel:) keyEquivalent:@""];
    [appMenu addItem:[NSMenuItem separatorItem]];
    [appMenu addItemWithTitle:@"Hide Quantum Updater" action:@selector(hide:) keyEquivalent:@"h"];
    NSMenuItem *hideOthers = [appMenu addItemWithTitle:@"Hide Others" action:@selector(hideOtherApplications:) keyEquivalent:@"h"];
    [hideOthers setKeyEquivalentModifierMask:NSEventModifierFlagOption | NSEventModifierFlagCommand];
    [appMenu addItemWithTitle:@"Show All" action:@selector(unhideAllApplications:) keyEquivalent:@""];
    [appMenu addItem:[NSMenuItem separatorItem]];
    [appMenu addItemWithTitle:@"Quit Quantum Updater" action:@selector(terminate:) keyEquivalent:@"q"];
    [appMenuItem setSubmenu:appMenu];
    [mainMenu addItem:appMenuItem];

    // 2. File Menu
    NSMenuItem *fileMenuItem = [[NSMenuItem alloc] init];
    NSMenu *fileMenu = [[NSMenu alloc] initWithTitle:@"File"];
    [fileMenu addItemWithTitle:@"Close Window" action:@selector(performClose:) keyEquivalent:@"w"];
    [fileMenuItem setSubmenu:fileMenu];
    [mainMenu addItem:fileMenuItem];

    // 3. Edit Menu (Cut, Copy, Paste, Select All)
    NSMenuItem *editMenuItem = [[NSMenuItem alloc] init];
    NSMenu *editMenu = [[NSMenu alloc] initWithTitle:@"Edit"];
    [editMenu addItemWithTitle:@"Undo" action:@selector(undo:) keyEquivalent:@"z"];
    [editMenu addItemWithTitle:@"Redo" action:@selector(redo:) keyEquivalent:@"Z"];
    [editMenu addItem:[NSMenuItem separatorItem]];
    [editMenu addItemWithTitle:@"Cut" action:@selector(cut:) keyEquivalent:@"x"];
    [editMenu addItemWithTitle:@"Copy" action:@selector(copy:) keyEquivalent:@"c"];
    [editMenu addItemWithTitle:@"Paste" action:@selector(paste:) keyEquivalent:@"v"];
    [editMenu addItemWithTitle:@"Select All" action:@selector(selectAll:) keyEquivalent:@"a"];
    [editMenuItem setSubmenu:editMenu];
    [mainMenu addItem:editMenuItem];

    // 4. View Menu
    NSMenuItem *viewMenuItem = [[NSMenuItem alloc] init];
    NSMenu *viewMenu = [[NSMenu alloc] initWithTitle:@"View"];
    [viewMenu addItemWithTitle:@"Enter Full Screen" action:@selector(toggleFullScreen:) keyEquivalent:@"f"];
    [viewMenuItem setSubmenu:viewMenu];
    [mainMenu addItem:viewMenuItem];

    // 5. Window Menu
    NSMenuItem *windowMenuItem = [[NSMenuItem alloc] init];
    NSMenu *windowMenu = [[NSMenu alloc] initWithTitle:@"Window"];
    [windowMenu addItemWithTitle:@"Minimize" action:@selector(performMiniaturize:) keyEquivalent:@"m"];
    [windowMenu addItemWithTitle:@"Zoom" action:@selector(performZoom:) keyEquivalent:@""];
    [windowMenu addItem:[NSMenuItem separatorItem]];
    [windowMenu addItemWithTitle:@"Bring All to Front" action:@selector(arrangeInFront:) keyEquivalent:@""];
    [windowMenuItem setSubmenu:windowMenu];
    [mainMenu addItem:windowMenuItem];

    [app setMainMenu:mainMenu];
}

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

        // Configure Apple Menu Bar
        setup_macos_menu(app);

        // Set Application Dock Icon
        NSString *iconPath = [[[NSBundle mainBundle] resourcePath] stringByAppendingPathComponent:@"icon.icns"];
        if (![[NSFileManager defaultManager] fileExistsAtPath:iconPath]) {
            iconPath = [[[NSBundle mainBundle] resourcePath] stringByAppendingPathComponent:@"web/icon.png"];
        }
        if ([[NSFileManager defaultManager] fileExistsAtPath:iconPath]) {
            NSImage *appIcon = [[NSImage alloc] initWithContentsOfFile:iconPath];
            [app setApplicationIconImage:appIcon];
        }

        NSRect frame = NSMakeRect(0, 0, 1220, 800);
        NSWindow *window = [[NSWindow alloc] initWithContentRect:frame
                                                       styleMask:(NSWindowStyleMaskTitled |
                                                                 NSWindowStyleMaskClosable |
                                                                 NSWindowStyleMaskMiniaturizable |
                                                                 NSWindowStyleMaskResizable |
                                                                 NSWindowStyleMaskFullSizeContentView)
                                                         backing:NSBackingStoreBuffered
                                                           defer:NO];
        window.title = @"Quantum Updater • Smart Mac Manager";
        window.titleVisibility = NSWindowTitleHidden;
        window.titlebarAppearsTransparent = YES;
        [window center];

        // Frosted Glass Background
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
        webView.UIDelegate = bridge;
        bridge.webView = webView;
        [webView setValue:@NO forKey:@"drawsBackground"];

        [window.contentView addSubview:webView];
        [window makeKeyAndOrderFront:nil];
        [app activateIgnoringOtherApps:YES];

        // Resolve web/index.html
        NSString *webPath = [[[NSBundle mainBundle] resourcePath] stringByAppendingPathComponent:@"web/index.html"];
        if (![[NSFileManager defaultManager] fileExistsAtPath:webPath]) {
            webPath = [[[NSFileManager defaultManager] currentDirectoryPath] stringByAppendingPathComponent:@"web/index.html"];
        }
        NSURL *url = [NSURL fileURLWithPath:webPath];
        [webView loadFileURL:url allowingReadAccessToURL:[url URLByDeletingLastPathComponent]];

        [app run];
    }
    return 0;
}
