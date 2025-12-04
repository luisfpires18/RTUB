# iOS App Store Submission Guide

This document provides a comprehensive guide for preparing and submitting the RTUB PWA to the Apple App Store as a native iOS application.

## Table of Contents

- [Overview](#overview)
- [iOS PWA Capabilities](#ios-pwa-capabilities)
- [Preparation Checklist](#preparation-checklist)
- [Method 1: PWABuilder (Recommended)](#method-1-pwabuilder-recommended)
- [Method 2: Manual Xcode Project](#method-2-manual-xcode-project)
- [iOS-Specific Optimizations](#ios-specific-optimizations)
- [App Store Requirements](#app-store-requirements)
- [Testing on iOS](#testing-on-ios)
- [Submission Process](#submission-process)
- [Post-Submission](#post-submission)
- [Troubleshooting](#troubleshooting)

## Overview

RTUB can be packaged as a native iOS app using one of two methods:
1. **PWABuilder** (Easiest) - Automatically generates an Xcode project
2. **Manual Xcode Project** - More control but requires Swift/Objective-C knowledge

Both methods wrap your PWA in a native iOS WKWebView container, providing:
- Native app icon on home screen
- App Store distribution
- Push notification support (with additional setup)
- Native iOS UI integration

## iOS PWA Capabilities

### What Works Well
✅ **Offline functionality** - Service worker caching  
✅ **Add to Home Screen** - Direct installation from Safari  
✅ **Standalone mode** - Full-screen without Safari UI  
✅ **Touch icons** - 180x180 Apple touch icon configured  
✅ **Status bar styling** - Matches app theme  
✅ **Splash screens** - Auto-generated from icons and theme colors  

### iOS Limitations
⚠️ **Push notifications** - Require native code and APNs setup  
⚠️ **Background sync** - Limited compared to Android  
⚠️ **Service worker** - Some restrictions on iOS Safari  
⚠️ **Install prompt** - No automatic install banner (manual Add to Home Screen)  

## Preparation Checklist

Before proceeding with App Store submission:

- [ ] Deploy PWA to production with HTTPS
- [ ] Verify PWA works correctly on iOS Safari
- [ ] Test "Add to Home Screen" functionality
- [ ] Prepare app metadata (description, keywords, screenshots)
- [ ] Create App Store Connect account ($99/year)
- [ ] Prepare app privacy policy URL
- [ ] Prepare support URL
- [ ] Create marketing materials (1024x1024 icon, screenshots)
- [ ] Decide on app name and bundle identifier

### Required Assets

1. **App Icon** (1024x1024 px, PNG, no alpha channel)
   - Use existing RTUB logo: `/icons/rtub-logo-512.png` (needs upscaling)
   - Must be exactly 1024x1024 pixels
   - No transparency, no rounded corners (iOS adds them automatically)

2. **Screenshots** (Required for different device sizes)
   - iPhone 6.7" (1290 x 2796 px) - iPhone 15 Pro Max
   - iPhone 6.5" (1242 x 2688 px) - iPhone 11 Pro Max
   - iPhone 5.5" (1242 x 2208 px) - iPhone 8 Plus
   - iPad Pro 12.9" (2048 x 2732 px) - Optional but recommended

3. **App Privacy Information**
   - What data is collected
   - How data is used
   - Whether data is shared with third parties

## Method 1: PWABuilder (Recommended)

PWABuilder provides the easiest path to App Store submission by automatically generating an Xcode project.

### Step 1: Generate iOS Package

1. Visit [PWABuilder](https://www.pwabuilder.com)
2. Enter production URL: `https://rtub.azurewebsites.net`
3. Click "Package for Stores"
4. Select "iOS"

### Step 2: Configure iOS Package

**Basic Settings:**
- **Bundle ID**: `com.braganca.rtub` (must be globally unique)
- **App Name**: "RTUB"
- **App Version**: `1.0.0`
- **Build Number**: `1`

**Advanced Settings:**
- **Minimum iOS Version**: 14.0 or higher (recommended: 15.0)
- **Status Bar Style**: Black translucent (matches current config)
- **Launch Screen**: Use app colors and icon
- **URL Scheme**: `rtub://` (for deep linking)

### Step 3: Download and Extract

1. Click "Generate" to create the iOS package
2. Download the ZIP file
3. Extract to a local directory
4. Open the `.xcodeproj` file in Xcode

### Step 4: Configure in Xcode

1. **Signing & Capabilities**
   - Select your development team
   - Enable automatic signing
   - Configure capabilities:
     - Push Notifications (if needed)
     - Associated Domains (for deep linking)

2. **Info.plist Configuration**
   - Verify `CFBundleIdentifier`: `com.braganca.rtub`
   - Set `CFBundleDisplayName`: `RTUB`
   - Set `CFBundleShortVersionString`: `1.0.0`
   - Set `NSAppTransportSecurity` to allow HTTPS to your domain

3. **App Icons**
   - Add 1024x1024 icon to Assets.xcassets
   - Xcode generates all required sizes automatically

### Step 5: Build and Test

1. **Select a simulator or device**
   - iOS 15.0+ recommended
   - Test on both iPhone and iPad

2. **Build the app** (Cmd+B)
   - Fix any compilation errors
   - Verify bundle identifier is unique

3. **Run on simulator** (Cmd+R)
   - Test all major features
   - Verify offline functionality
   - Test navigation and authentication

### Step 6: Archive for Distribution

1. Select "Any iOS Device (arm64)" as build target
2. Product → Archive
3. Wait for archive to complete
4. Click "Distribute App"
5. Select "App Store Connect"
6. Follow prompts to upload to App Store Connect

## Method 2: Manual Xcode Project

For developers who want more control over the native wrapper.

### Prerequisites

- Xcode 14+ installed
- Active Apple Developer Program membership ($99/year)
- Basic Swift/Objective-C knowledge
- Understanding of WKWebView

### Step 1: Create Xcode Project

1. Open Xcode
2. File → New → Project
3. Select "App" under iOS
4. Configure:
   - Product Name: `RTUB`
   - Team: Your developer team
   - Organization Identifier: `com.braganca`
   - Bundle Identifier: `com.braganca.rtub`
   - Interface: SwiftUI or Storyboard
   - Language: Swift

### Step 2: Implement WKWebView Wrapper

**ContentView.swift** (SwiftUI approach):

```swift
import SwiftUI
import WebKit

struct ContentView: View {
    var body: some View {
        WebView(url: URL(string: "https://rtub.azurewebsites.net")!)
            .edgesIgnoringSafeArea(.all)
    }
}

struct WebView: UIViewRepresentable {
    let url: URL
    
    func makeUIView(context: Context) -> WKWebView {
        let configuration = WKWebViewConfiguration()
        configuration.allowsInlineMediaPlayback = true
        configuration.mediaTypesRequiringUserActionForPlayback = []
        
        let webView = WKWebView(frame: .zero, configuration: configuration)
        webView.navigationDelegate = context.coordinator
        
        // Enable service worker support
        if #available(iOS 14.0, *) {
            webView.configuration.preferences.setValue(true, forKey: "serviceWorkersEnabled")
        }
        
        return webView
    }
    
    func updateUIView(_ webView: WKWebView, context: Context) {
        let request = URLRequest(url: url)
        webView.load(request)
    }
    
    func makeCoordinator() -> Coordinator {
        Coordinator()
    }
    
    class Coordinator: NSObject, WKNavigationDelegate {
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            // Handle page load completion
        }
        
        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
            // Handle navigation errors
            print("Navigation error: \(error.localizedDescription)")
        }
    }
}
```

### Step 3: Configure Info.plist

Add these keys to `Info.plist`:

```xml
<key>NSAppTransportSecurity</key>
<dict>
    <key>NSExceptionDomains</key>
    <dict>
        <key>rtub.azurewebsites.net</key>
        <dict>
            <key>NSExceptionAllowsInsecureHTTPLoads</key>
            <false/>
            <key>NSIncludesSubdomains</key>
            <true/>
            <key>NSExceptionMinimumTLSVersion</key>
            <string>TLSv1.2</string>
        </dict>
    </dict>
</dict>

<key>UILaunchStoryboardName</key>
<string>LaunchScreen</string>

<key>UIStatusBarStyle</key>
<string>UIStatusBarStyleLightContent</string>

<key>UIViewControllerBasedStatusBarAppearance</key>
<false/>
```

### Step 4: Add Launch Screen

Create a launch screen that matches your PWA:
- Background color: `#3F2A86` (theme color)
- Center RTUB logo
- Optional: Loading indicator

### Step 5: Configure Signing

1. Select project in Xcode
2. Select "RTUB" target
3. Go to "Signing & Capabilities"
4. Select your team
5. Enable "Automatically manage signing"
6. Verify bundle identifier is unique

### Step 6: Test and Archive

Follow Step 5 and 6 from Method 1 above.

## iOS-Specific Optimizations

### Already Implemented

The RTUB PWA already includes iOS optimizations:

✅ **Apple Touch Icon** (180x180):
```html
<link rel="apple-touch-icon" sizes="180x180" href="/icons/rtub-logo-180.png" />
```

✅ **Web App Capable**:
```html
<meta name="apple-mobile-web-app-capable" content="yes" />
```

✅ **Status Bar Style**:
```html
<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
```

✅ **Theme Color**:
```html
<meta name="theme-color" content="#3F2A86" />
```

### Additional Recommendations

1. **Add Apple Touch Startup Images** (Optional)

For better user experience, add launch images for different device sizes:

```html
<!-- iPhone X/11/12/13 Pro (1125x2436) -->
<link rel="apple-touch-startup-image" 
      href="/launch-1125x2436.png" 
      media="(device-width: 375px) and (device-height: 812px) and (-webkit-device-pixel-ratio: 3)">

<!-- iPhone 8 Plus/7 Plus/6s Plus (1242x2208) -->
<link rel="apple-touch-startup-image" 
      href="/launch-1242x2208.png" 
      media="(device-width: 414px) and (device-height: 736px) and (-webkit-device-pixel-ratio: 3)">
```

2. **Add Pinned Tab Icon** (Safari Desktop)

```html
<link rel="mask-icon" href="/safari-pinned-tab.svg" color="#3F2A86">
```

3. **Optimize Viewport for iOS**

Already configured in `App.razor`:
```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
```

## App Store Requirements

### Technical Requirements

- ✅ HTTPS only (production deployment)
- ✅ 64-bit architecture support
- ✅ iOS 14.0+ minimum version (recommended)
- ✅ No private APIs used
- ✅ App Store Review Guidelines compliance

### Content Requirements

1. **App Name**: "RTUB" or "Real Tuna Universitária de Bragança"
   - Must be unique in App Store
   - Max 30 characters

2. **Subtitle**: Brief description (max 30 characters)
   - Example: "Tuna Management Platform"

3. **Description**: Full app description (max 4000 characters)
   ```
   RTUB is the official management platform for Real Tuna Universitária de Bragança, 
   a traditional Portuguese university music ensemble.

   Features:
   • Event and performance management
   • Member directory and profiles
   • Rehearsal scheduling and attendance
   • Music repertoire library
   • Financial management and tracking
   • Photo gallery and media sharing
   • Inventory management for instruments
   • Real-time notifications

   Perfect for managing tuna activities, coordinating members, and staying connected 
   with the Real Tuna Universitária de Bragança community.
   ```

4. **Keywords**: Comma-separated (max 100 characters)
   ```
   tuna, music, university, management, events, rehearsal, bragança, portugal
   ```

5. **Privacy Policy URL**: **REQUIRED**
   - Must be hosted on your domain
   - Example: `https://rtub.azurewebsites.net/privacy`
   - Explain what data is collected and how it's used

6. **Support URL**: Contact information for support
   - Example: `https://rtub.azurewebsites.net/support`

### Age Rating

Complete the age rating questionnaire in App Store Connect:
- Recommended: 4+ (No objectionable content)
- Based on content of the app

### App Category

- **Primary**: Music
- **Secondary**: Education

## Testing on iOS

### Safari Web Inspector

1. Enable on device: Settings → Safari → Advanced → Web Inspector
2. Connect device to Mac via USB
3. Open Safari on Mac → Develop → [Your Device] → RTUB
4. Use console to debug JavaScript and service worker

### iOS Simulator Testing

1. Open Xcode
2. Window → Devices and Simulators
3. Select simulator (iPhone 15, iOS 17+)
4. Install and run app
5. Test all features thoroughly

### TestFlight Beta Testing

Before public release, use TestFlight:

1. Upload build to App Store Connect
2. Complete beta app information
3. Add internal testers (up to 100)
4. Invite external testers (up to 10,000)
5. Collect feedback and fix issues
6. Upload new builds as needed

## Submission Process

### Step 1: Prepare App Store Connect

1. Go to [App Store Connect](https://appstoreconnect.apple.com)
2. Click "My Apps" → "+" → "New App"
3. Fill in app information:
   - Platform: iOS
   - Name: RTUB
   - Primary Language: Portuguese (Portugal)
   - Bundle ID: com.braganca.rtub
   - SKU: RTUB2025 (unique identifier)

### Step 2: Upload Build

1. Archive app in Xcode (Product → Archive)
2. Distribute App → App Store Connect
3. Wait for processing (10-30 minutes)
4. Build appears in App Store Connect

### Step 3: Complete App Information

**App Information:**
- Name: RTUB
- Subtitle: Tuna Management Platform
- Privacy Policy URL: (required)
- Category: Music, Education
- Content Rights: (explain any third-party content)

**Pricing and Availability:**
- Price: Free
- Availability: Worldwide (or specific countries)

**App Privacy:**
- Complete privacy questionnaire
- Specify data types collected
- Explain data usage

**Version Information:**
- Screenshots (see requirements above)
- Description
- Keywords
- Support URL
- Marketing URL (optional)
- Version: 1.0.0
- Copyright: © 2025 Real Tuna Universitária de Bragança

### Step 4: Submit for Review

1. Select uploaded build
2. Add "What's New in This Version" notes
3. Complete Export Compliance questionnaire
4. Add notes for reviewer (optional but helpful):
   ```
   Test Account Credentials:
   Username: [provide test username]
   Password: [provide test password]
   
   This is a Progressive Web App wrapped in a native container.
   The app connects to https://rtub.azurewebsites.net
   ```
5. Submit for review

### Step 5: Review Process

- **Initial Review**: 24-48 hours typically
- **Possible Outcomes**:
  - ✅ Approved: App goes live
  - ⚠️ Metadata Rejected: Fix app info, resubmit (fast)
  - ❌ Binary Rejected: Fix code, upload new build, resubmit

Common rejection reasons:
- Missing privacy policy
- Misleading screenshots
- Crashes or bugs
- Incomplete functionality
- Guideline violations

## Post-Submission

### After Approval

1. **App is Live**: Available on App Store
2. **Monitor**: Check reviews and ratings
3. **Respond**: Reply to user reviews
4. **Update**: Release updates for bugs/features

### Updating the App

When you update your PWA:

1. **Automatic Content Updates**: Content updates automatically (it's a web app!)
2. **Native Wrapper Updates**: Only needed for:
   - iOS version requirements change
   - New native features
   - Bug fixes in wrapper code

To submit updates:
1. Increment version number (1.0.0 → 1.0.1)
2. Increment build number (1 → 2)
3. Archive and upload new build
4. Add "What's New" description
5. Submit for review

### Marketing

- Add App Store badge to website
- Share App Store link on social media
- Encourage existing users to download
- Collect and showcase positive reviews

## Troubleshooting

### Build Errors

**"Provisioning profile doesn't include signing certificate"**
- Solution: Regenerate provisioning profile in developer portal
- Or: Enable automatic signing in Xcode

**"Bundle identifier is already in use"**
- Solution: Choose different bundle ID (e.g., `com.braganca.rtub.app`)

**"Minimum iOS version not met"**
- Solution: Set minimum deployment target in Xcode project settings

### Runtime Issues

**App shows blank screen**
- Check: HTTPS URL is correct
- Check: Network connectivity
- Check: Console logs in Safari Web Inspector

**Service worker not working**
- iOS Safari has limitations on service workers
- Test thoroughly on actual devices
- Consider fallback for offline functionality

**Authentication issues**
- Check: Cookies are enabled
- Check: Session persistence in WKWebView
- May need to handle authentication tokens differently

### Rejection Issues

**Guideline 4.2: Minimum Functionality**
- Solution: Ensure app provides meaningful functionality
- Add native features if needed
- Explain value proposition clearly

**Guideline 2.1: App Completeness**
- Solution: Test thoroughly before submission
- Fix all crashes and major bugs
- Provide working test account

**Guideline 5.1.1: Privacy**
- Solution: Include privacy policy
- Complete privacy questionnaire accurately
- Implement data protection measures

## Additional Resources

### Official Documentation
- [Apple Developer Program](https://developer.apple.com/programs/)
- [App Store Connect](https://appstoreconnect.apple.com)
- [App Store Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)
- [Human Interface Guidelines](https://developer.apple.com/design/human-interface-guidelines/)

### PWA on iOS
- [iOS Safari Web App Capabilities](https://developer.apple.com/library/archive/documentation/AppleApplications/Reference/SafariWebContent/ConfiguringWebApplications/ConfiguringWebApplications.html)
- [PWABuilder Documentation](https://docs.pwabuilder.com/#/builder/app-store)

### Tools
- [PWABuilder](https://www.pwabuilder.com) - Generate iOS package
- [Xcode](https://developer.apple.com/xcode/) - Build and test
- [App Store Connect](https://appstoreconnect.apple.com) - Manage app
- [TestFlight](https://testflight.apple.com) - Beta testing

## Summary

**Recommended Approach:**
1. Use PWABuilder to generate iOS package (fastest)
2. Test thoroughly on iOS devices and simulators
3. Create App Store Connect account ($99/year)
4. Prepare all required assets and metadata
5. Submit for TestFlight beta testing
6. Gather feedback and fix issues
7. Submit for App Store review
8. Monitor reviews and update as needed

**Key Differences from Android:**
- Higher barrier to entry ($99/year vs $25 one-time)
- Stricter review process
- More stringent guidelines
- Limited PWA capabilities compared to Android
- No automatic content updates notification

**Timeline Estimate:**
- PWABuilder setup: 1-2 hours
- Asset preparation: 2-4 hours
- Testing and fixes: 1-2 days
- App Store Connect setup: 1-2 hours
- Review process: 1-3 days
- **Total: ~1 week** from start to App Store

---

**Last Updated**: December 2025  
**Version**: 1.0.0  
**Contact**: [@luisfpires18](https://github.com/luisfpires18)
