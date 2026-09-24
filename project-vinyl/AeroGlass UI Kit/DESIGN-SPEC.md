# Aero Glass UI — Design System Specification

Canonical reference for the Emi Toolkit visual identity. All tokens, gradients, and component behaviors defined here are authoritative. The XAML resource dictionary (`AeroGlassResources.xaml`) is the machine-readable implementation of this spec.

## 1. Color Tokens

### Base Palette (Color resources)

| Token | Hex | Role |
|-------|-----|------|
| `cDeep` | `#07405E` | Primary text, headings, deep accents |
| `cInk` | `#0B5C8A` | Interactive text, links, secondary emphasis |
| `cSky1` | `#BFE9FB` | Light sky accent |
| `cSky2` | `#7CCBEE` | Medium sky accent |
| `cBlue1` | `#6FC7F0` | Light blue highlight |
| `cBlue2` | `#1F86C8` | **Primary brand color**, selection, active states |
| `cGreen1` | `#B6E77A` | Light green highlight |
| `cGreen2` | `#4CA22B` | Success, positive actions |
| `cAmber1` | `#FFD97A` | Light amber highlight |
| `cAmber2` | `#E08C1E` | Warning, cautionary actions |
| `cRed1` | `#FFA694` | Light red highlight |
| `cRed2` | `#C63C22` | Error, destructive actions, revert |

### Derived Brushes (SolidColorBrush)

| Token | Value | Usage |
|-------|-------|-------|
| `Deep` | `cDeep` solid | All primary text |
| `Ink` | `cInk` solid | Secondary interactive text |
| `Muted` | `#2C5F7E` solid | Subtitles, descriptions, metadata |
| `Line` | `#FFFFFF` @ 40% | Dividers, subtle borders |
| `Glass` | `#FFFFFF` @ 55% | Panel backgrounds over SkyBrush |
| `Glass2` | `#FFFFFF` @ 35% | Lighter glass for nested containers |

### Gradient Brushes

#### SkyBrush (Background)
Diagonal linear gradient from top-left to bottom-right. **Mandatory root background.**

```
StartPoint: 0.12,0 → EndPoint: 0.72,1
Offset 0.00: #E3F4FDFF (pale blue-white)
Offset 0.30: #D895D9F6 (soft sky)
Offset 0.60: #D24DBBEC (medium sky)
Offset 0.82: #D267D0C4 (sky-mint transition)
Offset 1.00: #D897DC5F (mint-green)
```

#### TopGloss (Overlay)
Vertical white fade applied as a 170px-tall rectangle at the top of the window. Simulates overhead lighting on glass.

```
Offset 0.00: #A0FFFFFF (63% white)
Offset 0.55: #42FFFFFF (26% white)
Offset 1.00: #00FFFFFF (transparent)
```

#### Gloss Blue / Green / Amber / Red (Buttons)
Vertical gradient with a **hard stop at 49%→51%** to create the signature glossy reflection. Each variant follows the same structure:

```
Offset 0.00: Light tint (highlight)
Offset 0.49: Mid-tone (just above the fold)
Offset 0.51: Dark tone (just below the fold) ← HARD STOP
Offset 1.00: Slightly lighter than mid-tone (bounce light)
```

| Variant | Offset 0 | Offset 0.49 | Offset 0.51 | Offset 1 |
|---------|----------|-------------|-------------|----------|
| GlossBlue | `#8FD9F7` | `#39A5DC` | `#1B7FC0` | `#39B0E4` |
| GlossGreen | `#CBF08F` | `#74C23C` | `#4E9C22` | `#7FCF43` |
| GlossAmber | `#FFE6A8` | `#F0A93A` | `#D5871B` | `#F7B950` |
| GlossRed | `#FFB6A6` | `#DD5C3E` | `#B63B22` | `#E0704F` |

#### Shine (Button inner highlight)
Applied as a 17px-tall border at the top of glossy buttons. Creates the "wet look" reflection.

```
Offset 0.00: #B8FFFFFF (72% white)
Offset 0.48: #38FFFFFF (22% white)
Offset 0.50: #00FFFFFF (transparent) ← sharp cutoff
```

#### CardBrush (Container background)
Subtle white-to-sky gradient for card surfaces.

```
Offset 0.00: #EDFFFFFF (93% white)
Offset 0.45: #C4FFFFFF (77% white)
Offset 1.00: #B0EBF8FF (69% white + sky tint)
```

## 2. Typography

- **Family:** `Segoe UI` (mandatory for Windows-native feel)
- **Rendering:** `TextFormattingMode="Ideal"` + `UseLayoutRounding="True"` + `SnapsToDevicePixels="True"`
- **Monospace:** `Consolas` for logs and code output

| Style Key | Size | Weight | Foreground | Usage |
|-----------|------|--------|------------|-------|
| `H1` | 25px | Light | `Deep` | Page titles |
| `H2` | 15px | SemiBold | `Deep` | Section headers within cards |
| Body (Nav) | 13.5px | Regular / SemiBold (selected) | `Deep` | Navigation labels, body text |
| `Sub` | 12.5px | Regular | `Muted` | Descriptions, hints, metadata |
| Log | 11.5px | Regular | `Deep` | Console/log output (Consolas) |

## 3. Elevation & Shadows

All elevated elements use the same `SoftShadow` effect:

```xml
<DropShadowEffect BlurRadius="16" ShadowDepth="2"
                  Direction="270" Opacity="0.28" Color="#FF0A3A56"/>
```

Key properties:
- **Color is dark blue (`#0A3A56`), never black or gray.** This integrates shadows into the scene's color temperature.
- **BlurRadius 16** produces a soft, diffused shadow appropriate for glass surfaces.
- **Direction 270** (straight down) simulates overhead lighting consistent with TopGloss.
- Applied to: Gloss buttons, Cards, Switch knob.

## 4. Component Specifications

### Gloss Button (`Style Key="Gloss"`)
- Height: 40px (standard), 62px (`GlossBig`)
- CornerRadius: 9 (outer border), 10 (glow ring)
- Border: `#59FFFFFF` 1px → `#B3FFFFFF` on hover
- Inner shine: 17px tall, top-aligned, `Shine` brush
- Pressed: TranslateY +1px, opacity 0.88
- Disabled: opacity 0.45, cursor Arrow
- Glow ring: transparent by default, `#3DFFFFFF` on hover

### Ghost Button (`Style Key="Ghost"`)
- Height: 34px
- Background: `#66FFFFFF` → `#AAFFFFFF` on hover
- Border: `#80FFFFFF` 1px
- CornerRadius: 8
- Disabled: opacity 0.4

### Chrome Button (`Style Key="Chrome"`)
- Size: 44×32px
- For window controls (minimize, maximize, close)
- Transparent background → `#7AFFFFFF` on hover
- CornerRadius: 7

### Navigation RadioButton (`Style Key="Nav"`)
- Height: 42px, Margin: 10,3
- Selected state: gradient fill (`#E6FFFFFF` → `#A6E7F7FF`) + 4px left bar in `GlossBlue`
- Hover: `#4DFFFFFF` background
- Font becomes SemiBold when selected
- Icon: `NavIcon` style (19×19, GlossBlue fill, white stroke 0.7px)

### Toggle Switch (`Style Key="Switch"`)
- Track: 52×26px, CornerRadius 13
- Off: `#59FFFFFF` track
- On: `GlossGreen` track overlay fades in
- Knob: 20×20 ellipse with white-to-pale-blue gradient + SoftShadow
- Knob slides from left (margin 3,0,0,0) to right (margin 0,0,3,0)

### CheckBox (`Style Key="Check"`)
- Box: 19×19, CornerRadius 5
- Unchecked: `#B3FFFFFF` bg, `#8C2E86B8` border
- Checked: `GlossBlue` fill + white checkmark path (stroke 2.2, round caps)
- Hover: border changes to `#1F86C8`
- Disabled: opacity 0.45

### Card (`Style Key="Card"`)
- CornerRadius: 14
- Background: `CardBrush`
- Border: `#80FFFFFF` 1px
- Padding: 18px
- Effect: `SoftShadow`

### ProgressBar (`Style Key="Bar"`)
- Height: 10px
- Track: CornerRadius 6, `#59FFFFFF` bg, `#73FFFFFF` border
- Indicator: `GlossBlue`, CornerRadius 5, with 4px white highlight at top

### TextBox (`Style Key="Field"`)
- Height: 34px
- Background: `#B3FFFFFF`
- Border: `#8CFFFFFF` 1px
- CornerRadius: 8
- Padding: 10,0

### ListView (`Style Key="Lst"`)
- Background: `#40FFFFFF`
- Border: `#66FFFFFF` 1px
- Row height: 34px
- Row hover: `#59FFFFFF`
- Row selected: `#73D6F0FF`
- Header: `#4DFFFFFF` bg, SemiBold 12px

### TabControl / TabItem
- Tab: CornerRadius 9,9,0,0; padding 16,7
- Inactive: `#3DFFFFFF` bg
- Hover: `#6BFFFFFF`
- Selected: `#B8FFFFFF` bg, `#8CFFFFFF` border, SemiBold

### ScrollBar
- Width: 10px
- Thumb: CornerRadius 5, `#7A2E86B8`
- Track: `#1AFFFFFF`
- Repeat buttons: invisible (opacity 0)

## 5. Window Structure

The canonical window layout has three layers:

1. **SkyBrush** rectangle filling the entire window
2. **TopGloss** rectangle (170px tall, top-aligned, non-hit-testable)
3. **Decorative bubbles**: asymmetric radial-gradient ellipses at varying opacities (22%–30%) positioned to break visual monotony

Window properties:
- `WindowStyle="None"` + `WindowChrome` for custom title bar
- `AllowsTransparency="False"` (hardware-accelerated glass via composition)
- `Background="Transparent"` (lets SkyBrush show through chrome)
- CaptionHeight: 46px
- MinHeight: 620, MinWidth: 980

## 6. Decorative Elements

### Bubble Pattern
RadialGradientBrush with off-center origin (0.35, 0.3):
```
Offset 0.00: #FFFFFFFF (white center)
Offset 0.55-0.60: tinted midtone (varies per bubble)
Offset 1.00: #00XXXXXX (transparent edge)
```

Place at least 2 bubbles asymmetrically. Typical positions:
- Large: left edge, vertically centered (~300×300, opacity 0.30)
- Medium: top-right corner (~380×380, opacity 0.26)
- Small: lower-center (~180×180, opacity 0.22)

## 7. Portability Notes

When porting to non-WPF frameworks:

- **Web/CSS:** Use `backdrop-filter: blur(12px)` for glass panels. Pre-render glossy button gradients as SVG or CSS `linear-gradient` with hard stops at 49%/51%.
- **WinUI/MAUI:** Most brushes map directly. Replace `DropShadowEffect` with `ThemeShadow` or equivalent.
- **Flutter:** Use `BoxDecoration` with `gradient: LinearGradient(stops: [0.49, 0.51])`. Glass effect via `BackdropFilter` + `ImageFilter.blur`.
- **Never substitute black shadows.** The colored shadow (`#0A3A56`) is essential to the aesthetic.
- **Never use solid white backgrounds.** Every surface must have alpha or be a gradient.