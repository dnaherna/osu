# Mania touch layouts

Open Settings, select osu!mania, and choose **Edit touch layout**. Set the
mobile layout to Landscape or Portrait before entering the editor.

The preview shows the entire gameplay surface at the current screen aspect
ratio. Drag the numbered rectangles with a finger or mouse. The numbers
correspond to note columns from left to right; moving a rectangle does not move
its note column. Select a rectangle to change its width and height. Positions
snap to a 5% grid; disable snapping for free placement. Sizes change in 2.5%
steps. Keep rectangles separate to avoid ambiguous input where they overlap.

Select the key count (4 by default; 1–18 available). **Save** saves only the
currently selected key count and orientation and enables the touch overlay.
Reopen the editor to save another key count. **Cancel** or Back discards changes.
**Reset**, followed by Save, restores the original touch overlay for the current
key count and orientation. Layouts persist in mania's settings, independently
of skins. Counts without a saved layout retain the original overlay.

Layouts use relative screen coordinates. If you change device/aspect ratio,
check the preview and adjust the positions to suit the new screen.

# Building an iPhone IPA without a Mac

The upstream `Continuous Integration` workflow has an iOS build check, but
does not upload an IPA and uses GitHub's `xcode-27` runner image.

This fork adds **Build unsigned iOS IPA**, a manual workflow using GitHub-hosted
`xcode-27` runners with Xcode `27.0` selected by default. Push these changes to your fork, enable Actions if prompted,
then open Actions → Build unsigned iOS IPA → Run workflow. Select the branch
containing these changes and leave the Xcode version field at `27.0`.
Leave Configuration at **Release** to connect to the normal osu! account server.
Debug connects to the separate development server.
Download the `osu-ios-unsigned` artifact from the
completed run and extract the `.ipa`.

Like the upstream iOS CI check, this workflow uses `dotnet build`, but defaults
to Release for production-server login. It builds only `osu.iOS.csproj` rather than `osu.iOS.slnf`, which also
contains test apps. It adds `ios-arm64`, unsigned signing options, and
`BuildIpa=true` so the output is a device IPA rather than only a build check.
The workflow overrides the repo's Release defaults with `TrimMode=copy` and
`MtouchInterpreter=all` for both configurations. These avoid removing unused
managed code and AOT-compiling managed assemblies, addressing the Release build
that timed out after the assembly-stripping messages. The IPA will be larger,
and interpreter gameplay performance is not representative of an optimized AOT
Release build. The iOS processing pipeline and native packaging still run.

Normal-verbosity build output and an MSBuild binary log are enabled. The
`osu-ios-build-diagnostics` artifact is uploaded even if the build fails or
times out, when the runner can finish the upload and the log exists. The last
assembly named in console output is not necessarily the task causing a stall;
use the binary log to inspect build targets and timings if this happens again.

The workflow requires no Apple signing secrets. The IPA is unsigned: it must
be signed/provisioned by your iOS sideloading tool before installation. It is
not a TestFlight/App Store build. A successful cloud run and installation on a
physical device are still required to verify this workflow and iOS touch UX.

The installed .NET iOS workload and Xcode must be compatible. The workflow logs
the selected Xcode version. The Xcode 27 runner is a GitHub-hosted preview image,
not a custom/self-hosted runner. Selecting Xcode 27 does not change the project's
minimum supported iOS version (15.0), so iOS 18 remains within that range.
If a future workload requires another Xcode version, supply a matching
installed Xcode version in the workflow input; if a required version is not yet
on GitHub's hosted image, the runner/workload will need updating.

References:
- [Microsoft iOS build properties](https://learn.microsoft.com/en-us/dotnet/ios/building-apps/build-properties)
- [GitHub macOS runner images](https://github.com/actions/runner-images)
- [GitHub Xcode 27 runner announcement](https://github.com/actions/runner-images/issues/14404)
