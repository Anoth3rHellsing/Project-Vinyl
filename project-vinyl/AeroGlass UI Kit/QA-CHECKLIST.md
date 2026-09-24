# Aero Glass UI — QA Verification Checklist

Protocol for AI QA agents to verify that a WPF implementation complies with the Aero Glass design system. Use this as the prompt basis when spawning verification agents.

## Agent Invocation Template

```
Audit [path/to/target.xaml] against .claude/skills/aero-glass-ui/DESIGN-SPEC.md
and .claude/skills/aero-glass-ui/QA-CHECKLIST.md.

For each violation found, report using ReportFindings with:
- file: relative path to the XAML file
- line: 1-indexed line number
- summary: one-sentence defect description
- failure_scenario: what breaks visually or functionally
- category: one of [visual-token, visual-gradient, visual-spacing, 
              interaction-state, accessibility, integration, typography]
- verdict: CONFIRMED (you verified in source) or PLAUSIBLE (inferred)

Rank findings most-severe first. Empty array if compliant.
```

## Dimension 1: Color Token Compliance

Verify every color reference resolves to a defined token. Flag any hardcoded hex values not in the palette.

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| Text foreground uses `Deep`, `Ink`, or `Muted` | No raw hex in Foreground setters | Text invisible on glass or inconsistent hierarchy |
| Button backgrounds use `Gloss*` brushes | No solid color fills on action buttons | Buttons look flat, lose glossy identity |
| Borders use alpha-white (`#59FFFFFF`–`#8CFFFFFF`) | No `#FFFFFF` solid borders | Harsh edges break glass illusion |
| Shadows use `#0A3A56` | No black/gray shadow colors | Elements appear disconnected from scene |
| Background is `SkyBrush` or derived | No solid white/gray window background | Loss of depth and atmosphere |

## Dimension 2: Gradient Fidelity

Glossy gradients are the signature element. Verify exact offset values.

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| Gloss buttons have hard stop at 0.49→0.51 | Offsets exactly 0.49 and 0.51 | Gradient looks smooth/muddy, loses shine |
| Shine brush cuts off at 0.50 | Offset 0.48 → 0.50 transition | Inner highlight bleeds, looks artificial |
| SkyBrush diagonal direction preserved | StartPoint ~0.12,0 EndPoint ~0.72,1 | Background looks flat or wrong angle |
| CardBrush ends with sky tint | Final stop includes blue/green channel | Cards look like plain white overlays |
| TopGloss fades to transparent | Final offset is `#00FFFFFF` | Hard edge visible at 170px boundary |

## Dimension 3: Spacing & Layout

Glass aesthetics require generous whitespace. Cramped layouts break the feel.

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| Card padding ≥ 14px | Padding="14" or greater | Content feels cramped against glass edges |
| Inter-element margin ≥ 8px | Margin between sibling controls | Elements visually collide |
| Navigation item height = 42px | Height="42" on Nav style | Touch targets too small or inconsistent |
| Button height matches variant | Gloss=40, GlossBig=62, Ghost=34 | Visual hierarchy breaks |
| Window MinHeight/MinWidth set | MinHeight≥620 MinWidth≥980 | Layout collapses at small sizes |

## Dimension 4: Interactive States

Every interactive control must define Hover, Pressed/Checked, and Disabled states.

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| Gloss button has IsMouseOver trigger | Glow + border brightening defined | No hover feedback, feels dead |
| Gloss button has IsPressed trigger | TranslateY=1 + opacity 0.88 | No press feedback, feels unresponsive |
| Gloss button has IsEnabled=False trigger | Opacity 0.45 + Arrow cursor | Disabled state indistinguishable |
| Nav RadioButton has IsChecked trigger | sel.Opacity=1 + bar.Opacity=1 | Selected page not indicated |
| CheckBox has IsChecked + IsMouseOver triggers | Fill/tick appear + border highlights | State changes invisible |
| Switch has IsChecked trigger | Knob slides + green track appears | Toggle state ambiguous |

## Dimension 5: Typography

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| FontFamily is Segoe UI | Set on Window or inherited | Non-native font breaks Windows feel |
| TextFormattingMode="Ideal" | Set on root Window | Text renders blurry on glass |
| SnapsToDevicePixels="True" | Set on root Window | Subpixel artifacts on borders/text |
| H1 is 25px Light | Matches spec exactly | Title hierarchy unclear |
| H2 is 15px SemiBold | Matches spec exactly | Section headers lack emphasis |
| Sub is 12.5px Muted color | Matches spec exactly | Secondary text competes with primary |
| Log text uses Consolas 11.5px | Monospace for code/log output | Proportional font misaligns log columns |

## Dimension 6: Accessibility

Glass UIs are notoriously hostile to accessibility. Verify compensations.

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| Deep-on-glass contrast ≥ 4.5:1 | `#07405E` on lightest glass stop passes WCAG AA | Text unreadable for low-vision users |
| Muted-on-glass contrast ≥ 3:1 | `#2C5F7E` on CardBrush passes large-text AA | Subtitles invisible |
| Focus indicators present | Keyboard focus visible on all interactive elements | Keyboard-only users cannot navigate |
| Disabled state distinguishable without color alone | Opacity change + cursor change | Colorblind users cannot tell disabled state |
| Touch targets ≥ 32px minimum dimension | All buttons/checks/switches meet size | Mobile/touch users cannot activate |

## Dimension 7: Integration & Resource Merging

| Check | Pass Criteria | Failure Scenario |
|-------|--------------|------------------|
| ResourceDictionary merged correctly | Source path valid, no duplicate keys | Styles silently fail to apply |
| BasedOn chains intact | Variant styles reference base Gloss style | Variants lose template/triggers |
| TemplateBinding used in ControlTemplates | `{TemplateBinding Background}` etc. | Style overrides ignored at runtime |
| StaticResource references resolve | All x:Key targets exist in dictionary | Runtime XamlParseException |
| WindowChrome configured | CaptionHeight=46, IsHitTestVisibleInChrome on chrome buttons | Title bar drag broken, buttons unclickable |
| Decorative layers non-hit-testable | `IsHitTestVisible="False"` on bubbles/gloss overlays | Clicks intercepted by decoration |

## Severity Classification

When reporting findings, classify severity:

| Severity | Definition | Example |
|----------|-----------|---------|
| Critical | App crashes or core function broken | Missing StaticResource causes XamlParseException |
| High | Visual identity fundamentally broken | Solid white background instead of SkyBrush |
| Medium | Noticeable deviation from spec | Gloss gradient offsets wrong by >0.02 |
| Low | Minor polish issue | Padding 12px instead of 14px |
| Info | Suggestion, not violation | Could add animation to state transitions |

## Automated Verification Script

For agents that can parse XAML programmatically, these regex patterns catch common violations:

```powershell
# Find hardcoded hex colors NOT in the approved palette
Select-String -Path "*.xaml" -Pattern '#[0-9A-Fa-f]{8}' | 
  Where-Object { $_.Line -notmatch '(cDeep|cInk|cBlue|cGreen|cAmber|cRed|SkyBrush|Gloss|Card|Shine|TopGloss|SoftShadow)' }

# Find gloss gradients without hard stop
Select-String -Path "*.xaml" -Pattern 'Offset="0\.5"' |
  Where-Object { $_.Line -match 'Gloss' }  # Should be 0.49 and 0.51, not 0.5

# Find solid white borders (should have alpha)
Select-String -Path "*.xaml" -Pattern 'BorderBrush="#FFFFFFFF"'

# Find black shadows (should be #0A3A56)
Select-String -Path "*.xaml" -Pattern 'Color="#[Ff][Ff]000000".*DropShadow'
```

## Post-Fix Re-verification

After applying fixes, re-run the full checklist. Set `outcome` on each finding:
- `fixed`: Verified resolved in source
- `skipped`: Acknowledged but deferred (document reason)
- `no_change_needed`: False positive after investigation