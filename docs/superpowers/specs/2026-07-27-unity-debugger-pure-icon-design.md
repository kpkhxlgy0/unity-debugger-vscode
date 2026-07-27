# Unity Debugger Pure Extension Icon Design

Date: 2026-07-27

## Objective

Create a distinctive extension icon for **Unity Debugger Pure**, integrate it
into the extension manifest, and prepare version `0.1.1` without implying an
official relationship with Unity Technologies or Unity China.

## Approved Visual Direction

The icon uses a minimal flat composition:

- a full-bleed deep navy square background;
- one centered cyan isometric wireframe cube;
- one solid red breakpoint replacing the cube's lower-right node;
- no text, letters, gradients, shadows, texture, or fine detail;
- no Unity, Tuanjie, Microsoft, VS Code, or other third-party logo geometry.

The cube suggests a game-engine scene or object without copying a vendor
mark. The breakpoint communicates the extension's single purpose: managed C#
debugging. Integrating the breakpoint into the cube keeps the symbol readable
at extension-list sizes and avoids making it look like a notification badge.

## Composition and Palette

The final asset is a square PNG at `512x512` pixels, exceeding the VS Code
manifest requirement of at least `128x128` and the recommended `256x256` size
for high-density displays.

Use these palette anchors:

- background: deep navy `#0B1220`;
- cube primary: cyan blue `#38BDF8`;
- cube secondary edge tone, if needed for face separation: `#0EA5E9`;
- breakpoint: debugger red `#F14C4C`.

The graphic remains inside a 12% safe area. Cube strokes and the breakpoint
must remain distinct at `128x128`, `64x64`, and `32x32`. The background fills
the complete square, so the asset does not require transparency or background
removal.

## Asset and Manifest Integration

The project-bound output is:

```text
images/icon.png
```

`package.json` will reference it with:

```json
"icon": "images/icon.png"
```

The extension version will advance from the already-published `0.1.0` to
`0.1.1`. Current build, package, checksum, release-verification, and Open VSX
workflow contracts that name the versioned VSIX will be updated consistently
to `unity-debugger-pure-0.1.1.vsix`. Historical specifications and the
immutable `v0.1.0` GitHub Release remain unchanged.

## Generation and Selection

Use the built-in image generation path to produce the approved mark. Generate
one clean square candidate first. Inspect the result for geometry, palette,
trademark distance, text absence, and small-size legibility. If iteration is
needed, make one targeted visual correction at a time rather than changing the
approved concept.

The selected image is copied into the repository and downscaled or normalized
to an exact `512x512` PNG when necessary. No generated asset may remain only
in the image tool's default output directory.

## Validation

Automated checks will verify:

- `package.json` declares version `0.1.1` and `icon: images/icon.png`;
- the icon exists, is PNG, and is exactly `512x512`;
- packaging includes the icon at the declared path;
- all operational VSIX and checksum contracts use `0.1.1` consistently;
- the existing build, extension, Adapter, integration, package, and workflow
  tests remain green.

Visual inspection will verify:

- the cube and breakpoint remain recognizable at `128`, `64`, and `32`
  pixels;
- the red dot reads as a breakpoint, not a notification counter;
- no third-party trademark or text appears;
- edges are clean and the dark background has no unintended gradient,
  texture, or watermark.

## Release Boundary

This work creates and integrates the icon and prepares the audited `0.1.1`
package. Creating a `v0.1.1` tag, GitHub Release, Marketplace upload, or Open
VSX publication remains a separately confirmed external release action.
