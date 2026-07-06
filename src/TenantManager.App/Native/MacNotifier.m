#import <Foundation/Foundation.h>
#import <objc/runtime.h>

typedef void (*NotificationCallback)();
static NotificationCallback g_callback = NULL;

__attribute__((visibility("default")))
void init_mac_notifier(NotificationCallback callback) {
    g_callback = callback;
}

@interface NotifierDelegate : NSObject <NSUserNotificationCenterDelegate>
@end

@implementation NotifierDelegate
- (BOOL)userNotificationCenter:(NSUserNotificationCenter *)center shouldPresentNotification:(NSUserNotification *)notification {
    return YES;
}

- (void)userNotificationCenter:(NSUserNotificationCenter *)center didActivateNotification:(NSUserNotification *)notification {
    if (notification.activationType == NSUserNotificationActivationTypeActionButtonClicked ||
        notification.activationType == NSUserNotificationActivationTypeContentsClicked) {
        if (g_callback) {
            g_callback();
        }
    }
}
@end

__attribute__((visibility("default")))
void show_mac_notification(const char* title, const char* body) {
    NSString *nsTitle = [NSString stringWithUTF8String:title];
    NSString *nsBody = [NSString stringWithUTF8String:body];

    dispatch_async(dispatch_get_main_queue(), ^{
        @autoreleasepool {
            NSUserNotification *notification = [[NSUserNotification alloc] init];
            // Unique identifier prevents macOS from deduplicating across launches
            notification.identifier = [[NSUUID UUID] UUIDString];
            notification.title = nsTitle;
            notification.informativeText = nsBody;
            notification.soundName = NSUserNotificationDefaultSoundName;
            notification.hasActionButton = YES;
            notification.actionButtonTitle = @"Mostrar";
            // Force delivery date to now so macOS treats it as a fresh notification
            notification.deliveryDate = [NSDate date];

            NSUserNotificationCenter *center = [NSUserNotificationCenter defaultUserNotificationCenter];

            static NotifierDelegate *delegate = nil;
            static dispatch_once_t onceToken;
            dispatch_once(&onceToken, ^{
                delegate = [[NotifierDelegate alloc] init];
                center.delegate = delegate;
            });

            [center deliverNotification:notification];
        }
    });
}

