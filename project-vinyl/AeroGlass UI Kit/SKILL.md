---
name: aero-glass-ui
description: Apply the Emi Toolkit Aero Glass design system to WPF apps and run QA verification agents. Use when creating new WPF UIs, restyling existing ones, or verifying visual/functional compliance with this design system. Triggers on "aero glass", "emi style", "glossy ui", "wpf design system", "verify ui", "qa check".
---

# Aero Glass UI Design System & QA

This skill applies the **Emi Toolkit Aero Glass** design system to any WPF application and orchestrates AI QA agents to verify the result.

## When to Use

- Creating a new WPF window/page that must match the Emi Toolkit aesthetic
- Restyling an existing WPF app to use this design system
- Verifying that a UI implementation matches the spec (visual + functional)
- Porting this style to other XAML-based frameworks (WinUI, MAUI)

## Quick Start

### 1. Apply the Design System

Copy the canonical resource dictionary into your project:

```powershell
Copy-Item ".claude/skills/aero-glass-ui/AeroGlassResources.xaml" -Destination "YourProject/Resources/" -Force
```

Then merge it in your `App.xaml` or `Window.Resources`:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="Resources/AeroGlassResources.xaml"/>
</ResourceDictionary.MergedDictionaries>
```

### 2. Verify Compliance

Spawn the QA agent to audit your XAML against the design system:

```
Use the Agent tool with subagent_type "general-purpose" and prompt:
"Audit [path/to/your.xaml] against .claude/skills/aero-glass-ui/DESIGN-SPEC.md. 
Report violations as a typed list using ReportFindings."
```

## Design System Reference

The full specification lives in [DESIGN-SPEC.md](DESIGN-SPEC.md). Key tokens:

| Token | Value | Usage |
|-------|-------|-------|
| `cDeep` | `#07405E` | Primary text, headings |
| `cBlue2` | `#1F86C8` | Primary actions, selection |
| `cGreen2` | `#4CA22B` | Success states, positive actions |
| `cAmber2` | `#E08C1E` | Warnings, cautionary actions |
| `cRed2` | `#C63C22` | Errors, destructive actions |
| `SkyBrush` | Diagonal gradient | Mandatory background layer |
| `Gloss*` | Gradient brushes | Button fills (hard stop at 50%) |
| `CardBrush` | White-to-sky gradient | Card/container backgrounds |

## Component Checklist

Every compliant UI must include:

- [ ] `SkyBrush` as root background (never solid white/gray)
- [ ] `TopGloss` overlay on top 170px
- [ ] Decorative bubble ellipses (at least 2, asymmetric)
- [ ] `Gloss` button style for primary actions
- [ ] `Card` border style for content containers
- [ ] `Nav` RadioButton style for sidebar navigation
- [ ] `Field` TextBox style for inputs
- [ ] `Bar` ProgressBar style for progress indicators
- [ ] Segoe UI font family with `TextFormattingMode="Ideal"`
- [ ] `SoftShadow` DropShadowEffect on elevated elements

## QA Verification Protocol

When verifying a UI implementation, check these dimensions:

### Visual Compliance
1. **Color accuracy**: All colors match DESIGN-SPEC.md tokens exactly
2. **Gradient fidelity**: Gloss buttons have hard stop at offset 0.49→0.51
3. **Transparency**: No solid white backgrounds; all containers use alpha
4. **Typography**: Correct sizes, weights, and foreground colors per hierarchy
5. **Spacing**: Minimum 14px padding in cards, 10px margins between elements
6. **Shadows**: Colored shadows (`#0A3A56`), never black/gray

### Functional Compliance
1. **Interactive states**: Hover, Pressed, Disabled all defined and correct
2. **Navigation**: Selected state shows bar indicator + gradient fill
3. **Focus management**: Keyboard navigation works, focus visible
4. **Accessibility**: Contrast ratios meet WCAG AA on glass backgrounds
5. **Responsiveness**: Layout respects MinHeight/MinWidth constraints

### Integration Compliance
1. **Resource merging**: Dictionary properly merged, no duplicate keys
2. **Style inheritance**: BasedOn chains intact for variants
3. **Template bindings**: ControlTemplates use TemplateBinding correctly
4. **Triggers**: All Property triggers have EnterActions/ExitActions if animated

## Gotchas

- **Never use `AllowsTransparency="True"`** unless you need per-pixel opacity. It disables hardware rendering and causes performance issues. The Emi Toolkit uses `False` with `WindowChrome` for glass effects.
- **Gloss gradients require exact offsets**. The hard stop at 0.49→0.51 is what creates the "shine" effect. Offsets like 0.48→0.52 look muddy.
- **White borders must have alpha**. `#FFFFFF` solid borders look harsh on glass. Always use `#59FFFFFF` to `#8CFFFFFF`.
- **Shadows are colored, not neutral**. Using black shadows makes elements look disconnected from the scene. Always use `#0A3A56`.
- **Segoe UI is mandatory**. Other fonts break the Windows-native feel. If targeting cross-platform, map to system sans-serif but keep metrics identical.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Buttons look flat | Missing hard stop in gradient | Verify offsets are exactly 0.49 and 0.51 |
| Background looks washed out | Solid white instead of SkyBrush | Replace with `{StaticResource SkyBrush}` |
| Text blurry on glass | Wrong text formatting mode | Set `TextOptions.TextFormattingMode="Ideal"` |
| Shadows look harsh | Black shadow color | Change to `#0A3A56` |
| Cards feel cramped | Insufficient padding | Minimum 14px, prefer 18px |
| Navigation doesn't highlight | Missing trigger for IsChecked | Add trigger setting `sel.Opacity=1` and `bar.Opacity=1` |

## Files

- [DESIGN-SPEC.md](DESIGN-SPEC.md) — Complete design system specification
- [AeroGlassResources.xaml](AeroGlassResources.xaml) — Canonical resource dictionary (copy this)
- [QA-CHECKLIST.md](QA-CHECKLIST.md) — Detailed QA verification checklist for agents