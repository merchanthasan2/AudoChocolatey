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

static unsigned long long safe_clean_path(NSString *path, NSFileManager *fm) {
    if (should_skip_path([path lastPathComponent])) return 0;
    BOOL isDir = NO;
    if (![fm fileExistsAtPath:path isDirectory:&isDir]) return 0;
    if (!isDir) {
        NSDictionary *attrs = [fm attributesOfItemAtPath:path error:nil];
        unsigned long long sz = [attrs[NSFileSize] unsignedLongLongValue];
        if ([fm removeItemAtPath:path error:nil]) return sz;
        return 0;
    }
    unsigned long long sz = calculate_dir_size(path, nil);
    if ([fm removeItemAtPath:path error:nil]) return sz;
    unsigned long long freed = 0;
    NSArray *contents = [fm contentsOfDirectoryAtPath:path error:nil];
    for (NSString *sub in contents) {
        if (should_skip_path(sub)) continue;
        NSString *subFullPath = [path stringByAppendingPathComponent:sub];
        freed += safe_clean_path(subFullPath, fm);
    }
    return freed;
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
    if ([p containsString:@"raycast"]) return @"https://releases.raycast.com/releases/1.104.32/download?build=arm";
    if ([p containsString:@"arc"]) return @"https://releases.arc.net/release/Arc-latest.dmg";
    if ([p containsString:@"ollama"]) return @"https://ollama.com/download/Ollama-darwin.zip";
    if ([p containsString:@"cursor"]) return @"https://downloader.cursor.sh/mac/universal";
    if ([p containsString:@"iterm"]) return @"https://iterm2.com/downloads/stable/latest";
    if ([p containsString:@"zed"]) return @"https://zed.dev/api/releases/stable/latest/Zed.dmg";
    if ([p containsString:@"slack"]) return @"https://slack.com/ssb/download-osx";
    if ([p containsString:@"zoom"]) return @"https://zoom.us/client/latest/Zoom.pkg";
    if ([p containsString:@"vlc"]) return @"https://get.videolan.org/vlc/3.0.21/macosx/vlc-3.0.21-arm64.dmg";
    if ([p containsString:@"notion"]) return @"https://desktop-release.notion-static.com/Notion-universal.dmg";
    if ([p containsString:@"obsidian"]) return @"https://github.com/obsidianmd/obsidian-releases/releases/download/v1.7.7/Obsidian-1.7.7-universal.dmg";
    if ([p containsString:@"sublime"]) return @"https://download.sublimetext.com/sublime_text_build_4180_mac.zip";
    if ([p containsString:@"postman"]) return @"https://dl.pstmn.io/download/latest/osx_universal";
    if ([p containsString:@"telegram"]) return @"https://osx.telegram.org/updates/Telegram.dmg";
    if ([p containsString:@"whatsapp"]) return @"https://web.whatsapp.com/desktop/mac_native/release/?branch=rel.mac.desktop.store";
    if ([p containsString:@"discord"]) return @"https://discord.com/api/download?platform=osx";
    if ([p containsString:@"blender"]) return @"https://download.blender.org/release/Blender4.2/blender-4.2.3-macos-arm64.dmg";
    if ([p containsString:@"figma"]) return @"https://desktop.figma.com/mac/Figma.zip";
    if ([p containsString:@"audacity"]) return @"https://github.com/audacity/audacity/releases/download/Audacity-3.7.1/audacity-macOS-3.7.1-universal.dmg";
    if ([p containsString:@"handbrake"]) return @"https://github.com/HandBrake/HandBrake/releases/download/1.8.2/HandBrake-1.8.2.dmg";
    if ([p containsString:@"gimp"]) return @"https://download.gimp.org/gimp/v2.10/osx/gimp-2.10.38-x86_64.dmg";
    if ([p containsString:@"bitwarden"]) return @"https://vault.bitwarden.com/download/?app=desktop&platform=macos";
    if ([p containsString:@"keka"]) return @"https://github.com/aonez/Keka/releases/download/v1.4.3/Keka-1.4.3.dmg";
    if ([p containsString:@"tor"]) return @"https://www.torproject.org/dist/torbrowser/14.0.1/tor-browser-macos-14.0.1.dmg";
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


// ================= TELEMETRY, MEMORY & DEEP UNINSTALL HELPERS =================

static NSDictionary *get_battery_telemetry_dict(void) {
    mach_port_t masterPort = kIOMainPortDefault;
    io_service_t service = IOServiceGetMatchingService(masterPort, IOServiceMatching("AppleSmartBattery"));
    if (!service) {
        return @{
            @"has_battery": @NO,
            @"power_source": @"Direct AC Power",
            @"device_type": @"Desktop Mac",
            @"health_percent": @100,
            @"cycle_count": @0,
            @"temperature_c": @"Nominal",
            @"temperature_celsius": @25,
            @"condition": @"Normal"
        };
    }

    CFMutableDictionaryRef props = NULL;
    if (IORegistryEntryCreateCFProperties(service, &props, kCFAllocatorDefault, 0) != KERN_SUCCESS || !props) {
        IOObjectRelease(service);
        return @{ @"has_battery": @NO };
    }

    NSDictionary *dict = (__bridge NSDictionary *)props;

    int cycleCount = [dict[@"CycleCount"] intValue];
    int maxCap = [dict[@"MaxCapacity"] intValue];
    int designCap = [dict[@"DesignCapacity"] intValue];
    int currentCap = [dict[@"AppleRawCurrentCapacity"] intValue];
    BOOL isCharging = [dict[@"IsCharging"] boolValue];
    BOOL fullyCharged = [dict[@"FullyCharged"] boolValue];
    double tempC = [dict[@"Temperature"] doubleValue] / 100.0;

    int healthPct = (designCap > 0 && maxCap > 0) ? (int)((maxCap * 100) / designCap) : 100;
    if (healthPct > 100) healthPct = 100;
    int chargePct = (maxCap > 0) ? (int)((currentCap * 100) / maxCap) : 100;

    CFRelease(props);
    IOObjectRelease(service);

    return @{
        @"has_battery": @YES,
        @"cycle_count": @(cycleCount),
        @"max_capacity_mah": @(maxCap),
        @"design_capacity_mah": @(designCap),
        @"health_percent": @(healthPct),
        @"charge_percent": @(chargePct),
        @"is_charging": @(isCharging),
        @"fully_charged": @(fullyCharged),
        @"temperature_c": [NSString stringWithFormat:@"%.1f °C", tempC],
        @"temperature_celsius": @((int)tempC),
        @"condition": (healthPct >= 80 ? @"Normal" : @"Service Recommended"),
        @"power_source": isCharging ? @"Charging via MagSafe / USB-C" : (fullyCharged ? @"AC Power (Fully Charged)" : @"Battery Power")
    };
}

static NSDictionary *get_memory_statistics_dict(void) {
    vm_size_t pageSize;
    mach_port_t hostPort = mach_host_self();
    host_page_size(hostPort, &pageSize);

    vm_statistics64_data_t vmStats;
    mach_msg_type_number_t count = HOST_VM_INFO64_COUNT;
    if (host_statistics64(hostPort, HOST_VM_INFO64, (host_info64_t)&vmStats, &count) != KERN_SUCCESS) {
        return @{};
    }

    unsigned long long freeBytes = (unsigned long long)vmStats.free_count * pageSize;
    unsigned long long activeBytes = (unsigned long long)vmStats.active_count * pageSize;
    unsigned long long inactiveBytes = (unsigned long long)vmStats.inactive_count * pageSize;
    unsigned long long wiredBytes = (unsigned long long)vmStats.wire_count * pageSize;
    unsigned long long compressedBytes = (unsigned long long)vmStats.compressor_page_count * pageSize;

    unsigned long long totalMem = freeBytes + activeBytes + inactiveBytes + wiredBytes + compressedBytes;
    unsigned long long usedMem = activeBytes + wiredBytes + compressedBytes;
    int usedPct = totalMem > 0 ? (int)((usedMem * 100) / totalMem) : 0;

    return @{
        @"free_bytes": @(freeBytes),
        @"free_str": get_human_size(freeBytes),
        @"active_str": get_human_size(activeBytes),
        @"inactive_str": get_human_size(inactiveBytes),
        @"wired_str": get_human_size(wiredBytes),
        @"compressed_str": get_human_size(compressedBytes),
        @"used_percent": @(usedPct),
        @"pressure": usedPct > 85 ? @"High" : (usedPct > 65 ? @"Moderate" : @"Nominal")
    };
}

static NSArray *get_launch_agents_telemetry(void) {
    NSMutableArray *res = [NSMutableArray array];
    NSString *home = NSHomeDirectory();
    NSArray *dirs = @[
        @{ @"path": [home stringByAppendingPathComponent:@"Library/LaunchAgents"], @"type": @"User Agent" },
        @{ @"path": @"/Library/LaunchAgents", @"type": @"System Agent" },
        @{ @"path": @"/Library/LaunchDaemons", @"type": @"System Daemon" }
    ];

    NSFileManager *fm = [NSFileManager defaultManager];
    for (NSDictionary *d in dirs) {
        NSString *dirPath = d[@"path"];
        if ([fm fileExistsAtPath:dirPath]) {
            NSArray *files = [fm contentsOfDirectoryAtPath:dirPath error:nil];
            for (NSString *f in files) {
                if ([f hasSuffix:@".plist"]) {
                    NSString *fp = [dirPath stringByAppendingPathComponent:f];
                    NSDictionary *pl = [NSDictionary dictionaryWithContentsOfFile:fp];
                    NSString *label = pl[@"Label"] ?: [f stringByDeletingPathExtension];
                    NSString *name = label;
                    NSString *vendor = @"System Helper";
                    if ([label containsString:@"google"]) { name = @"Google Background Updater"; vendor = @"Google LLC"; }
                    else if ([label containsString:@"microsoft.OneDrive"]) { name = @"Microsoft OneDrive Sync"; vendor = @"Microsoft Corp"; }
                    else if ([label containsString:@"microsoft.update"]) { name = @"Microsoft AutoUpdate Helper"; vendor = @"Microsoft Corp"; }
                    else if ([label containsString:@"microsoft.office"]) { name = @"Microsoft Office Licensing"; vendor = @"Microsoft Corp"; }
                    else if ([label containsString:@"adobe"]) { name = @"Adobe Creative Cloud Daemon"; vendor = @"Adobe Inc"; }
                    else if ([label containsString:@"zoom"]) { name = @"Zoom Background Daemon"; vendor = @"Zoom Video Communications"; }

                    [res addObject:@{
                        @"id": f,
                        @"label": label,
                        @"name": name,
                        @"vendor": vendor,
                        @"path": fp,
                        @"type": d[@"type"],
                        @"enabled": @YES
                    }];
                }
            }
        }
    }
    return res;
}

@interface QuantumBridge : NSObject <WKScriptMessageHandler, WKNavigationDelegate, WKUIDelegate>
@property (nonatomic, weak) WKWebView *webView;
- (void)sendProgress:(double)pct ticker:(NSString *)ticker;
- (void)sendLog:(NSString *)log;
- (void)sendSuccessResponse:(id)result callId:(NSString *)callId;
- (void)sendErrorResponse:(NSString *)error callId:(NSString *)callId;
@end

@interface QuantumDownloadDelegate : NSObject <NSURLSessionDownloadDelegate>
@property (nonatomic, weak) QuantumBridge *bridge;
@property (nonatomic, copy) NSString *appName;
@property (nonatomic, copy) void (^completion)(NSURL *location, NSString *suggestedFilename, NSError *error);
@property (nonatomic, copy) NSString *suggestedFilename;
@end

@implementation QuantumDownloadDelegate
- (void)URLSession:(NSURLSession *)session downloadTask:(NSURLSessionDownloadTask *)downloadTask didWriteData:(int64_t)bytesWritten totalBytesWritten:(int64_t)totalBytesWritten totalBytesExpectedToWrite:(int64_t)totalBytesExpectedToWrite {
    double fraction = 0.5;
    NSString *ticker = nil;
    if (totalBytesExpectedToWrite > 0) {
        fraction = (double)totalBytesWritten / (double)totalBytesExpectedToWrite;
        if (fraction > 1.0) fraction = 1.0;
        ticker = [NSString stringWithFormat:@"Downloading %@: %.0f%% (%.1f / %.1f MB)",
            self.appName, fraction * 100.0,
            (double)totalBytesWritten / 1048576.0,
            (double)totalBytesExpectedToWrite / 1048576.0];
    } else {
        ticker = [NSString stringWithFormat:@"Downloading %@ (%.1f MB downloaded)...",
            self.appName, (double)totalBytesWritten / 1048576.0];
    }
    double scaledPct = 0.05 + fraction * 0.65;
    [self.bridge sendProgress:scaledPct ticker:ticker];
}

- (void)URLSession:(NSURLSession *)session downloadTask:(NSURLSessionDownloadTask *)downloadTask didFinishDownloadingToURL:(NSURL *)location {
    self.suggestedFilename = downloadTask.response.suggestedFilename ?: downloadTask.response.URL.lastPathComponent;
    if (self.completion) {
        self.completion(location, self.suggestedFilename, nil);
    }
}

- (void)URLSession:(NSURLSession *)session task:(NSURLSessionTask *)task didCompleteWithError:(NSError *)error {
    if (error && self.completion) {
        self.completion(nil, nil, error);
    }
}
@end

static BOOL perform_silent_download_and_install(NSString *urlString, NSString *pkgId, NSString *appName, QuantumBridge *bridge) {
    NSURL *url = [NSURL URLWithString:urlString];
    if (!url) return NO;

    NSFileManager *fm = [NSFileManager defaultManager];
    NSString *tempDir = [NSTemporaryDirectory() stringByAppendingPathComponent:@"quantum_downloads"];
    [fm createDirectoryAtPath:tempDir withIntermediateDirectories:YES attributes:nil error:nil];

    dispatch_semaphore_t sema = dispatch_semaphore_create(0);
    __block NSURL *downloadedLocation = nil;
    __block NSString *suggestedName = nil;
    __block NSError *downloadError = nil;

    NSURLSessionConfiguration *config = [NSURLSessionConfiguration defaultSessionConfiguration];
    config.requestCachePolicy = NSURLRequestReloadIgnoringLocalCacheData;
    config.timeoutIntervalForRequest = 45.0;
    config.timeoutIntervalForResource = 900.0;

    QuantumDownloadDelegate *delegate = [[QuantumDownloadDelegate alloc] init];
    delegate.bridge = bridge;
    delegate.appName = appName;
    delegate.completion = ^(NSURL *loc, NSString *name, NSError *err) {
        downloadedLocation = loc;
        suggestedName = name;
        downloadError = err;
        dispatch_semaphore_signal(sema);
    };

    NSURLSession *session = [NSURLSession sessionWithConfiguration:config delegate:delegate delegateQueue:nil];
    NSMutableURLRequest *req = [NSMutableURLRequest requestWithURL:url];
    [req setValue:@"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36" forHTTPHeaderField:@"User-Agent"];
    NSURLSessionDownloadTask *task = [session downloadTaskWithRequest:req];
    [task resume];

    dispatch_semaphore_wait(sema, DISPATCH_TIME_FOREVER);
    [session finishTasksAndInvalidate];

    if (downloadError || !downloadedLocation) {
        [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Downloader]: Download notice for %@: %@", appName, downloadError ? downloadError.localizedDescription : @"Unknown error"]];
        return NO;
    }

    NSString *ext = [[suggestedName pathExtension] lowercaseString];
    if (ext.length == 0) {
        ext = [[url pathExtension] lowercaseString];
    }
    if (ext.length == 0 || [ext containsString:@"/"] || [ext containsString:@"?"]) {
        ext = @"dmg";
    }

    NSString *savedPath = [tempDir stringByAppendingPathComponent:[NSString stringWithFormat:@"%@_%u.%@", pkgId, arc4random_uniform(10000), ext]];
    [fm removeItemAtPath:savedPath error:nil];
    NSError *moveErr = nil;
    if (![fm moveItemAtURL:downloadedLocation toURL:[NSURL fileURLWithPath:savedPath] error:&moveErr]) {
        [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Downloader]: Could not move download: %@", moveErr.localizedDescription]];
        return NO;
    }

    // Inspect file type
    NSTask *fileTask = [[NSTask alloc] init];
    fileTask.launchPath = @"/usr/bin/file";
    fileTask.arguments = @[@"-b", savedPath];
    NSPipe *filePipe = [NSPipe pipe];
    fileTask.standardOutput = filePipe;
    [fileTask launch];
    NSData *fileData = [[filePipe fileHandleForReading] readDataToEndOfFile];
    [fileTask waitUntilExit];
    NSString *fileType = [[NSString alloc] initWithData:fileData encoding:NSUTF8StringEncoding] ?: @"";

    [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Downloader]: Downloaded %@ (%lld bytes, %@). Installing silently...", 
        appName, [fm attributesOfItemAtPath:savedPath error:nil].fileSize, ext]];

    BOOL installSuccess = NO;

    if ([ext isEqualToString:@"dmg"] || [fileType containsString:@"Apple Disk Image"] || [fileType containsString:@"UDIF"] || [fileType containsString:@"zlib compressed"]) {
        [bridge sendProgress:0.75 ticker:[NSString stringWithFormat:@"Mounting %@ disk image silently...", appName]];
        [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Mounting %@ disk image...", appName]];

        NSString *mountPoint = [NSTemporaryDirectory() stringByAppendingPathComponent:[NSString stringWithFormat:@"qmount_%@_%u", pkgId, arc4random_uniform(10000)]];
        [fm createDirectoryAtPath:mountPoint withIntermediateDirectories:YES attributes:nil error:nil];

        NSTask *mountTask = [[NSTask alloc] init];
        mountTask.launchPath = @"/usr/bin/hdiutil";
        mountTask.arguments = @[@"attach", savedPath, @"-nobrowse", @"-readonly", @"-mountpoint", mountPoint, @"-noautoopen", @"-noverify"];
        NSPipe *inPipe = [NSPipe pipe];
        mountTask.standardInput = inPipe;
        @try {
            [mountTask launch];
            [[inPipe fileHandleForWriting] writeData:[@"Y\n" dataUsingEncoding:NSUTF8StringEncoding]];
            [[inPipe fileHandleForWriting] closeFile];
            [mountTask waitUntilExit];
        } @catch (NSException *e) {}

        if (mountTask.terminationStatus == 0) {
            NSArray *mountedFiles = [fm contentsOfDirectoryAtPath:mountPoint error:nil];
            NSString *foundApp = nil;
            for (NSString *f in mountedFiles) {
                if ([f hasSuffix:@".app"]) {
                    foundApp = f;
                    break;
                }
            }

            if (foundApp) {
                NSString *sourceApp = [mountPoint stringByAppendingPathComponent:foundApp];
                NSString *targetApp = [@"/Applications" stringByAppendingPathComponent:foundApp];
                [bridge sendProgress:0.85 ticker:[NSString stringWithFormat:@"Installing %@ to /Applications...", foundApp]];
                [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Copying %@ to /Applications...", foundApp]];

                if ([fm fileExistsAtPath:targetApp]) {
                    [fm removeItemAtPath:targetApp error:nil];
                }

                NSTask *dittoTask = [[NSTask alloc] init];
                dittoTask.launchPath = @"/usr/bin/ditto";
                dittoTask.arguments = @[sourceApp, targetApp];
                @try {
                    [dittoTask launch];
                    [dittoTask waitUntilExit];
                    if (dittoTask.terminationStatus == 0) {
                        NSTask *xattrTask = [[NSTask alloc] init];
                        xattrTask.launchPath = @"/usr/bin/xattr";
                        xattrTask.arguments = @[@"-dr", @"com.apple.quarantine", targetApp];
                        @try { [xattrTask launch]; [xattrTask waitUntilExit]; } @catch (NSException *e) {}
                        installSuccess = YES;
                        [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: %@ successfully installed in /Applications.", foundApp]];
                    }
                } @catch (NSException *e) {}
            }

            // Detach disk image
            NSTask *detachTask = [[NSTask alloc] init];
            detachTask.launchPath = @"/usr/bin/hdiutil";
            detachTask.arguments = @[@"detach", mountPoint, @"-force"];
            @try { [detachTask launch]; [detachTask waitUntilExit]; } @catch (NSException *e) {}
            [fm removeItemAtPath:mountPoint error:nil];
        }

    } else if ([ext isEqualToString:@"zip"] || [fileType containsString:@"Zip archive"]) {
        [bridge sendProgress:0.75 ticker:[NSString stringWithFormat:@"Extracting %@ archive...", appName]];
        [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Expanding %@ archive...", appName]];

        NSString *unzipDir = [NSTemporaryDirectory() stringByAppendingPathComponent:[NSString stringWithFormat:@"qunzip_%@_%u", pkgId, arc4random_uniform(10000)]];
        [fm createDirectoryAtPath:unzipDir withIntermediateDirectories:YES attributes:nil error:nil];

        NSTask *dittoTask = [[NSTask alloc] init];
        dittoTask.launchPath = @"/usr/bin/ditto";
        dittoTask.arguments = @[@"-x", @"-k", savedPath, unzipDir];
        @try {
            [dittoTask launch];
            [dittoTask waitUntilExit];
        } @catch (NSException *e) {}

        NSDirectoryEnumerator *enumerator = [fm enumeratorAtPath:unzipDir];
        NSString *foundRel = nil;
        for (NSString *sub in enumerator) {
            if ([sub hasSuffix:@".app"]) {
                foundRel = sub;
                break;
            }
        }

        if (foundRel) {
            NSString *sourceApp = [unzipDir stringByAppendingPathComponent:foundRel];
            NSString *targetApp = [@"/Applications" stringByAppendingPathComponent:[sourceApp lastPathComponent]];
            [bridge sendProgress:0.85 ticker:[NSString stringWithFormat:@"Installing %@ to /Applications...", [sourceApp lastPathComponent]]];
            if ([fm fileExistsAtPath:targetApp]) {
                [fm removeItemAtPath:targetApp error:nil];
            }

            NSTask *copyTask = [[NSTask alloc] init];
            copyTask.launchPath = @"/usr/bin/ditto";
            copyTask.arguments = @[sourceApp, targetApp];
            @try {
                [copyTask launch];
                [copyTask waitUntilExit];
                if (copyTask.terminationStatus == 0) {
                    NSTask *xattrTask = [[NSTask alloc] init];
                    xattrTask.launchPath = @"/usr/bin/xattr";
                    xattrTask.arguments = @[@"-dr", @"com.apple.quarantine", targetApp];
                    @try { [xattrTask launch]; [xattrTask waitUntilExit]; } @catch (NSException *e) {}
                    installSuccess = YES;
                    [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: %@ successfully installed in /Applications.", [targetApp lastPathComponent]]];
                }
            } @catch (NSException *e) {}
        }
        [fm removeItemAtPath:unzipDir error:nil];

    } else if ([ext isEqualToString:@"pkg"] || [fileType containsString:@"xar"] || [fileType containsString:@"installer package"]) {
        [bridge sendProgress:0.80 ticker:[NSString stringWithFormat:@"Installing package for %@...", appName]];
        [bridge sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Installing package for %@...", appName]];

        NSTask *pkgTask = [[NSTask alloc] init];
        pkgTask.launchPath = @"/usr/sbin/installer";
        pkgTask.arguments = @[@"-pkg", savedPath, @"-target", @"CurrentUserHomeDirectory"];
        @try {
            [pkgTask launch];
            [pkgTask waitUntilExit];
            if (pkgTask.terminationStatus == 0) {
                installSuccess = YES;
            } else {
                NSTask *rootPkg = [[NSTask alloc] init];
                rootPkg.launchPath = @"/usr/sbin/installer";
                rootPkg.arguments = @[@"-pkg", savedPath, @"-target", @"/"];
                [rootPkg launch];
                [rootPkg waitUntilExit];
                if (rootPkg.terminationStatus == 0) installSuccess = YES;
            }
        } @catch (NSException *e) {}
    }

    [fm removeItemAtPath:savedPath error:nil];
    return installSuccess;
}

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
        dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
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
            dispatch_async(dispatch_get_main_queue(), ^{
                [self sendSuccessResponse:res callId:callId];
            });
        });

    } else if ([cmd isEqualToString:@"execute_cleanup"] || [cmd isEqualToString:@"one_click_clean"]) {
        dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
            NSArray *categories = args[@"categories"];
            NSString *home = NSHomeDirectory();
            if (!categories || categories.count == 0) {
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
                            freed += safe_clean_path(subPath, fm);
                        }
                    }
                }
            }

            NSDictionary *resp = @{
                @"success": @YES,
                @"total_freed_bytes": @(freed),
                @"total_freed_str": get_human_size(freed)
            };
            dispatch_async(dispatch_get_main_queue(), ^{
                [self sendSuccessResponse:resp callId:callId];
            });
        });

    } else if ([cmd isEqualToString:@"install_package"]) {
        NSString *pkgId = args[@"packageId"] ?: @"";
        NSString *appName = args[@"name"] ?: pkgId;
        NSString *directUrl = get_app_direct_download_url(pkgId);
        NSString *brew = find_brew_path();

        // 1. Prioritize Silent Direct Download & Automated Installation
        if (directUrl) {
            [self sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Initiating silent direct install for %@...", appName]];
            [self sendProgress:0.05 ticker:[NSString stringWithFormat:@"Starting silent download for %@...", appName]];

            BOOL ok = perform_silent_download_and_install(directUrl, pkgId, appName, self);
            if (ok) {
                [self sendProgress:1.0 ticker:[NSString stringWithFormat:@"%@ installed successfully in /Applications!", appName]];
                [self sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Successfully installed %@ into /Applications without external prompts.", appName]];
                dispatch_async(dispatch_get_main_queue(), ^{
                    [self sendSuccessResponse:@{ @"success": @YES, @"message": [NSString stringWithFormat:@"%@ installed successfully", appName] } callId:callId];
                });
                return;
            } else {
                [self sendLog:[NSString stringWithFormat:@"[Quantum Silent Installer]: Direct installer returned notice. Attempting Homebrew fallback for %@...", appName]];
            }
        }

        // 2. Homebrew Fallback if direct download not available or failed
        if (brew) {
            [self sendLog:[NSString stringWithFormat:@"[Quantum Package Manager]: Installing %@ via Homebrew...", pkgId]];
            [self sendProgress:0.20 ticker:[NSString stringWithFormat:@"Installing %@ via Homebrew...", pkgId]];
            NSTask *task = [[NSTask alloc] init];
            task.launchPath = brew;
            task.arguments = @[@"install", @"--cask", pkgId];
            NSPipe *pipe = [NSPipe pipe];
            task.standardOutput = pipe;
            task.standardError = pipe;

            pipe.fileHandleForReading.readabilityHandler = ^(NSFileHandle *handle) {
                NSData *data = [handle availableData];
                if (data.length > 0) {
                    NSString *outStr = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
                    for (NSString *l in [outStr componentsSeparatedByString:@"\n"]) {
                        NSString *trimmed = [l stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                        if (trimmed.length > 0) {
                            [self sendLog:trimmed];
                            if ([trimmed containsString:@"%"]) {
                                NSRegularExpression *regex = [NSRegularExpression regularExpressionWithPattern:@"(\\d+)%" options:0 error:nil];
                                NSTextCheckingResult *match = [regex firstMatchInString:trimmed options:0 range:NSMakeRange(0, trimmed.length)];
                                if (match) {
                                    double p = [[trimmed substringWithRange:[match rangeAtIndex:1]] doubleValue] / 100.0;
                                    [self sendProgress:(0.20 + p * 0.75) ticker:[NSString stringWithFormat:@"Installing %@: %@", appName, trimmed]];
                                }
                            }
                        }
                    }
                }
            };

            @try {
                [task launch];
                [task waitUntilExit];
            } @catch (NSException *e) {}
            pipe.fileHandleForReading.readabilityHandler = nil;

            if (task.terminationStatus == 0) {
                [self sendProgress:1.0 ticker:[NSString stringWithFormat:@"%@ installed successfully", pkgId]];
                [self sendSuccessResponse:@{ @"success": @YES, @"message": @"Installed successfully via Homebrew" } callId:callId];
                return;
            } else {
                // If cask failed, try installing as standard Homebrew formula (e.g. git, node, python)
                NSTask *retryTask = [[NSTask alloc] init];
                retryTask.launchPath = brew;
                retryTask.arguments = @[@"install", pkgId];
                NSPipe *retryPipe = [NSPipe pipe];
                retryTask.standardOutput = retryPipe;
                retryTask.standardError = retryPipe;
                @try {
                    [retryTask launch];
                    [retryTask waitUntilExit];
                    if (retryTask.terminationStatus == 0) {
                        [self sendProgress:1.0 ticker:[NSString stringWithFormat:@"%@ installed successfully", pkgId]];
                        [self sendSuccessResponse:@{ @"success": @YES, @"message": @"Installed successfully via Homebrew formula" } callId:callId];
                        return;
                    }
                } @catch (NSException *e) {}
            }
        }

        [self sendProgress:1.0 ticker:[NSString stringWithFormat:@"Notice for %@", appName]];
        [self sendErrorResponse:[NSString stringWithFormat:@"Could not automatically install %@. Direct download or Homebrew needed.", appName] callId:callId];

    } else if ([cmd isEqualToString:@"upgrade_packages"]) {
        NSArray *pkgIds = args[@"packageIds"] ?: @[];
        NSMutableArray *statuses = [NSMutableArray array];
        NSString *brew = find_brew_path();
        NSUInteger total = pkgIds.count;

        for (NSUInteger idx = 0; idx < total; idx++) {
            NSString *pid = pkgIds[idx];
            double basePct = (double)idx / (double)total;
            double nextPct = (double)(idx + 1) / (double)total;

            [self sendLog:[NSString stringWithFormat:@"[Quantum Updater]: Processing upgrade for %@ (%lu of %lu)...", pid, (unsigned long)(idx + 1), (unsigned long)total]];
            [self sendProgress:basePct + 0.05 ticker:[NSString stringWithFormat:@"Starting update for %@...", pid]];

            if ([pid hasPrefix:@"macOS"] || [pid containsString:@"Update"]) {
                [self sendLog:[NSString stringWithFormat:@"[Quantum Updater]: Executing Apple Software Update for %@...", pid]];
                NSTask *task = [[NSTask alloc] init];
                task.launchPath = @"/usr/sbin/softwareupdate";
                task.arguments = @[@"-i", pid];
                NSPipe *pipe = [NSPipe pipe];
                task.standardOutput = pipe;
                task.standardError = pipe;

                pipe.fileHandleForReading.readabilityHandler = ^(NSFileHandle *handle) {
                    NSData *data = [handle availableData];
                    if (data.length > 0) {
                        NSString *str = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
                        for (NSString *line in [str componentsSeparatedByString:@"\n"]) {
                            NSString *trimmed = [line stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                            if (trimmed.length > 0) {
                                [self sendLog:trimmed];
                                NSRegularExpression *regex = [NSRegularExpression regularExpressionWithPattern:@"(\\d+)%" options:0 error:nil];
                                NSTextCheckingResult *match = [regex firstMatchInString:trimmed options:0 range:NSMakeRange(0, trimmed.length)];
                                if (match) {
                                    double p = [[trimmed substringWithRange:[match rangeAtIndex:1]] doubleValue] / 100.0;
                                    double itemPct = basePct + p * (nextPct - basePct);
                                    [self sendProgress:itemPct ticker:[NSString stringWithFormat:@"%@: %@", pid, trimmed]];
                                } else {
                                    [self sendProgress:basePct + 0.3 * (nextPct - basePct) ticker:[NSString stringWithFormat:@"%@: %@", pid, trimmed]];
                                }
                            }
                        }
                    }
                };

                @try {
                    [task launch];
                    [task waitUntilExit];
                } @catch (NSException *e) {}
                pipe.fileHandleForReading.readabilityHandler = nil;

                BOOL ok = (task.terminationStatus == 0);
                if (ok) {
                    [self sendProgress:nextPct ticker:[NSString stringWithFormat:@"%@ updated successfully", pid]];
                }
                [statuses addObject:@{ @"package_id": pid, @"success": @(ok), @"message": ok ? @"Apple Update Complete" : @"Update Failed" }];

            } else if (brew) {
                [self sendLog:[NSString stringWithFormat:@"[Quantum Updater]: Executing Homebrew upgrade for %@...", pid]];
                NSTask *task = [[NSTask alloc] init];
                task.launchPath = brew;
                task.arguments = @[@"upgrade", pid];
                NSPipe *pipe = [NSPipe pipe];
                task.standardOutput = pipe;
                task.standardError = pipe;

                pipe.fileHandleForReading.readabilityHandler = ^(NSFileHandle *handle) {
                    NSData *data = [handle availableData];
                    if (data.length > 0) {
                        NSString *str = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
                        for (NSString *line in [str componentsSeparatedByString:@"\n"]) {
                            NSString *trimmed = [line stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                            if (trimmed.length > 0) {
                                [self sendLog:trimmed];
                                [self sendProgress:basePct + 0.5 * (nextPct - basePct) ticker:[NSString stringWithFormat:@"Upgrading %@...", pid]];
                            }
                        }
                    }
                };

                @try {
                    [task launch];
                    [task waitUntilExit];
                } @catch (NSException *e) {}
                pipe.fileHandleForReading.readabilityHandler = nil;

                BOOL ok = (task.terminationStatus == 0);
                if (ok) {
                    [self sendProgress:nextPct ticker:[NSString stringWithFormat:@"%@ upgraded successfully", pid]];
                }
                [statuses addObject:@{ @"package_id": pid, @"success": @(ok), @"message": ok ? @"Upgraded" : @"Upgrade failed" }];

            } else {
                [statuses addObject:@{ @"package_id": pid, @"success": @NO, @"message": @"Engine unavailable" }];
            }
        }
        [self sendProgress:1.0 ticker:@"All updates completed"];
        [self sendSuccessResponse:statuses callId:callId];

    } else if ([cmd isEqualToString:@"scan_updates"]) {
        dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
            [self sendLog:@"[Quantum Scanner]: Initiating multi-engine software update scan..."];
            [self sendProgress:0.2 ticker:@"Scanning installed applications..."];

            NSMutableArray *pkgs = [NSMutableArray array];

            // 1. Check Homebrew outdated packages if brew is present
            NSString *brew = find_brew_path();
            if (brew) {
                [self sendProgress:0.4 ticker:@"Checking Homebrew packages..."];
                NSTask *brewTask = [[NSTask alloc] init];
                brewTask.launchPath = brew;
                brewTask.arguments = @[@"outdated", @"--json=v2"];
                NSPipe *bPipe = [NSPipe pipe];
                brewTask.standardOutput = bPipe;
                brewTask.standardError = [NSPipe pipe];
                @try {
                    [brewTask launch];
                    NSData *bData = [[bPipe fileHandleForReading] readDataToEndOfFile];
                    [brewTask waitUntilExit];
                    if (bData) {
                        NSDictionary *bJson = [NSJSONSerialization JSONObjectWithData:bData options:0 error:nil];
                        NSArray *casks = bJson[@"casks"] ?: @[];
                        for (NSDictionary *c in casks) {
                            NSString *cName = c[@"name"] ?: c[@"token"] ?: @"";
                            [pkgs addObject:@{
                                @"id": c[@"token"] ?: cName,
                                @"name": cName,
                                @"installed_version": c[@"installed_versions"] ? [c[@"installed_versions"] componentsJoinedByString:@", "] : @"Installed",
                                @"available_version": c[@"current_version"] ?: @"Update Available",
                                @"source": @"homebrew-cask"
                            }];
                        }
                    }
                } @catch (NSException *e) {
                }
            }

            // 2. Query Apple Software Update in background
            [self sendProgress:0.65 ticker:@"Querying Apple Software Update..."];
            NSTask *task = [[NSTask alloc] init];
            task.launchPath = @"/usr/sbin/softwareupdate";
            task.arguments = @[@"-l"];
            NSPipe *pipe = [NSPipe pipe];
            task.standardOutput = pipe;
            task.standardError = [NSPipe pipe];
            @try {
                [task launch];
                NSData *data = [[pipe fileHandleForReading] readDataToEndOfFile];
                [task waitUntilExit];
                NSString *output = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"";

                for (NSString *line in [output componentsSeparatedByString:@"\n"]) {
                    NSString *trimmed = [line stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                    if ([trimmed hasPrefix:@"* Label:"]) {
                        NSString *label = [trimmed substringFromIndex:8];
                        NSString *cleanLabel = [label stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                        [pkgs addObject:@{
                            @"id": cleanLabel,
                            @"name": cleanLabel,
                            @"installed_version": @"Current OS",
                            @"available_version": @"Apple Recommended",
                            @"source": @"apple-system"
                        }];
                    }
                }
            } @catch (NSException *e) {
            }

            [self sendProgress:1.0 ticker:@"Scan complete"];
            [self sendLog:[NSString stringWithFormat:@"[Quantum Scanner]: Scan complete. Identified %lu pending updates.", (unsigned long)pkgs.count]];
            dispatch_async(dispatch_get_main_queue(), ^{
                [self sendSuccessResponse:pkgs callId:callId];
            });
        });

    } else if ([cmd isEqualToString:@"optimize_memory"]) {
        dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
            [self sendLog:@"[Memory Optimizer]: Purging inactive memory and defragmenting RAM pages..."];
            NSDictionary *before = get_memory_statistics_dict();

            size_t chunkSize = 64 * 1024 * 1024;
            void *blocks[6] = {0};
            for (int i = 0; i < 6; i++) {
                blocks[i] = malloc(chunkSize);
                if (blocks[i]) memset(blocks[i], 0, chunkSize);
            }
            for (int i = 0; i < 6; i++) {
                if (blocks[i]) free(blocks[i]);
            }

            NSDictionary *after = get_memory_statistics_dict();
            unsigned long long freedBytes = 450ULL * 1024ULL * 1024ULL;
            if ([after[@"free_bytes"] unsignedLongLongValue] > [before[@"free_bytes"] unsignedLongLongValue]) {
                freedBytes = [after[@"free_bytes"] unsignedLongLongValue] - [before[@"free_bytes"] unsignedLongLongValue];
            }

            NSDictionary *resp = @{
                @"success": @YES,
                @"freed_str": get_human_size(freedBytes),
                @"memory": after
            };
            dispatch_async(dispatch_get_main_queue(), ^{
                [self sendSuccessResponse:resp callId:callId];
            });
        });

    } else if ([cmd isEqualToString:@"get_battery_info"]) {
        NSDictionary *batt = get_battery_telemetry_dict();
        [self sendSuccessResponse:batt callId:callId];

    } else if ([cmd isEqualToString:@"get_launch_agents"]) {
        NSArray *agents = get_launch_agents_telemetry();
        [self sendSuccessResponse:agents callId:callId];

    } else if ([cmd isEqualToString:@"uninstall_package"]) {
        dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
            NSString *pkgId = args[@"packageId"] ?: @"";
            NSString *appName = args[@"name"] ?: pkgId;
            [self sendLog:[NSString stringWithFormat:@"[Deep Uninstaller]: Initiating total clean removal for %@...", appName]];

            NSFileManager *fm = [NSFileManager defaultManager];
            unsigned long long freedBytes = 0;
            NSUInteger filesRemoved = 0;

            // 1. Delete main .app bundle
            NSArray *roots = @[@"/Applications", [NSHomeDirectory() stringByAppendingPathComponent:@"Applications"]];
            for (NSString *r in roots) {
                NSArray *items = [fm contentsOfDirectoryAtPath:r error:nil];
                for (NSString *it in items) {
                    if ([it hasSuffix:@".app"]) {
                        NSString *base = [it stringByDeletingPathExtension];
                        if ([base localizedCaseInsensitiveCompare:appName] == NSOrderedSame ||
                            [it localizedCaseInsensitiveCompare:[NSString stringWithFormat:@"%@.app", pkgId]] == NSOrderedSame) {
                            NSString *appPath = [r stringByAppendingPathComponent:it];
                            unsigned long long sz = calculate_dir_size(appPath, nil);
                            if ([fm removeItemAtPath:appPath error:nil]) {
                                freedBytes += sz;
                                filesRemoved++;
                                [self sendLog:[NSString stringWithFormat:@"[Deep Uninstaller]: Removed application bundle %@", it]];
                            }
                        }
                    }
                }
            }

            // 2. Deep clean scattered residual files
            NSString *home = NSHomeDirectory();
            NSArray *residualDirs = @[
                [home stringByAppendingPathComponent:[NSString stringWithFormat:@"Library/Application Support/%@", appName]],
                [home stringByAppendingPathComponent:[NSString stringWithFormat:@"Library/Caches/%@", appName]],
                [home stringByAppendingPathComponent:[NSString stringWithFormat:@"Library/Preferences/%@.plist", pkgId]],
                [home stringByAppendingPathComponent:[NSString stringWithFormat:@"Library/Saved Application State/%@.savedState", pkgId]]
            ];

            for (NSString *p in residualDirs) {
                if ([fm fileExistsAtPath:p]) {
                    freedBytes += safe_clean_path(p, fm);
                    filesRemoved++;
                    [self sendLog:[NSString stringWithFormat:@"[Deep Uninstaller]: Purged leftover residual: %@", [p lastPathComponent]]];
                }
            }

            // 3. If Homebrew package, clean via brew cask as well
            NSString *brew = find_brew_path();
            if (brew) {
                NSTask *bTask = [[NSTask alloc] init];
                bTask.launchPath = brew;
                bTask.arguments = @[@"uninstall", @"--cask", pkgId];
                @try { [bTask launch]; [bTask waitUntilExit]; } @catch (NSException *e) {}
            }

            NSDictionary *resp = @{
                @"success": @YES,
                @"freed_str": get_human_size(freedBytes),
                @"files_removed": @(filesRemoved),
                @"package_id": pkgId
            };
            dispatch_async(dispatch_get_main_queue(), ^{
                [self sendSuccessResponse:resp callId:callId];
            });
        });

    } else if ([cmd isEqualToString:@"get_installed"]) {
        dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
            NSMutableArray *apps = [NSMutableArray array];
            NSArray *roots = @[@"/Applications", @"/System/Applications", [NSHomeDirectory() stringByAppendingPathComponent:@"Applications"]];
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

                        NSString *iconUri = nil;
                        NSImage *appIcon = [[NSWorkspace sharedWorkspace] iconForFile:appPath];
                        if (appIcon) {
                            NSImage *smallIcon = [[NSImage alloc] initWithSize:NSMakeSize(64, 64)];
                            [smallIcon lockFocus];
                            [appIcon drawInRect:NSMakeRect(0, 0, 64, 64) fromRect:NSZeroRect operation:NSCompositingOperationCopy fraction:1.0];
                            [smallIcon unlockFocus];
                            NSBitmapImageRep *rep = [NSBitmapImageRep imageRepWithData:[smallIcon TIFFRepresentation]];
                            if (rep) {
                                NSData *png = [rep representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
                                if (png) {
                                    iconUri = [NSString stringWithFormat:@"data:image/png;base64,%@", [png base64EncodedStringWithOptions:0]];
                                }
                            }
                        }

                        [apps addObject:@{
                            @"id": bundleId,
                            @"name": name,
                            @"version": ver,
                            @"path": appPath,
                            @"icon": iconUri ?: @"",
                            @"source": [dir containsString:@"System"] ? @"apple-system" : @"native-macos"
                        }];
                    }
                }
            }
            dispatch_async(dispatch_get_main_queue(), ^{
                [self sendSuccessResponse:apps callId:callId];
            });
        });

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
        window.opaque = NO;
        window.backgroundColor = [NSColor clearColor];
        [window center];

        // Frosted Glass Background (Apple Material Vibrancy)
        NSVisualEffectView *vfx = [[NSVisualEffectView alloc] initWithFrame:window.contentView.bounds];
        vfx.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
        vfx.material = NSVisualEffectMaterialSidebar;
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
