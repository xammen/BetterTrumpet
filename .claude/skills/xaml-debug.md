---
name: xaml-debug
description: Debug XAML layout issues
tags: [xaml, ui, debug, layout]
model: sonnet
---

# XAML Debug

Analyze and debug XAML layout issues.

## Usage

```
/xaml-debug <file-path> [element-name]
```

## What this skill does

1. **Read XAML:**
   - Reads the specified XAML file
   - Focuses on a specific element if provided

2. **Analyze Structure:**
   - Identifies layout containers (Grid, StackPanel, Canvas, etc.)
   - Checks Grid.Row/Column definitions
   - Analyzes positioning (Margin, Alignment, Panel.ZIndex)
   - Identifies potential issues

3. **Visual Representation:**
   - Creates ASCII diagram of the layout structure
   - Shows parent-child relationships
   - Highlights Z-Index layers

4. **Suggest Fixes:**
   - Points out common layout problems:
     - Missing Row/Column definitions
     - Overlapping elements without Z-Index
     - Incorrect positioning (absolute vs relative)
     - Alignment issues
   - Proposes corrections

## Examples

```bash
# Debug the entire FlyoutWindow layout
/xaml-debug EarTrumpet/UI/Views/FlyoutWindow.xaml

# Debug specific element (device header)
/xaml-debug EarTrumpet/UI/Views/FlyoutWindow.xaml device-header

# Debug a specific line range
/xaml-debug EarTrumpet/UI/Views/FlyoutWindow.xaml --lines 115-230
```

## Common Issues Detected

### 1. Missing Grid Row/Column Definitions
```xml
<!-- ❌ Problem: Items in same Grid without rows -->
<Grid>
    <TextBlock Text="A" />
    <TextBlock Text="B" />
</Grid>

<!-- ✅ Solution: Add row definitions -->
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <TextBlock Grid.Row="0" Text="A" />
    <TextBlock Grid.Row="1" Text="B" />
</Grid>
```

### 2. Z-Index Overlap Issues
```xml
<!-- ❌ Problem: Buttons overlay text without proper Z-Index -->
<Grid>
    <TextBlock Text="Title" />
    <Button HorizontalAlignment="Right" />
</Grid>

<!-- ✅ Solution: Use Z-Index or proper layout -->
<Grid>
    <TextBlock Text="Title" Panel.ZIndex="0" />
    <Button HorizontalAlignment="Right" Panel.ZIndex="1" />
</Grid>
```

### 3. Absolute vs Relative Positioning
```xml
<!-- ❌ Problem: Multiple elements with absolute Margin -->
<Grid>
    <Button Margin="0,0,42,2" HorizontalAlignment="Right" />
    <Button Margin="0,0,12,2" HorizontalAlignment="Right" />
</Grid>

<!-- ✅ Solution: Use Grid columns -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>
    <Button Grid.Column="1" Width="30" />
    <Button Grid.Column="2" Width="30" Margin="0,0,12,0" />
</Grid>
```

### 4. Alignment Conflicts
```xml
<!-- ❌ Problem: Conflicting alignment -->
<TextBlock HorizontalAlignment="Left" 
           Margin="12,0,78,0"
           TextTrimming="CharacterEllipsis" />

<!-- ✅ Solution: Let Grid handle sizing -->
<TextBlock Margin="12,0,12,0"
           TextTrimming="CharacterEllipsis" />
```

## Visual Layout Representation

When analyzing, the skill creates ASCII diagrams like:

```
Grid (ClipToBounds=False)
├─ Row 0 (Height=Auto) - Device Header
│  └─ Grid (3 columns)
│     ├─ Column 0 (*) - TextBlock (Device Name)
│     ├─ Column 1 (Auto) - Button (Expand/Collapse)
│     └─ Column 2 (Auto) - Button (Pin)
│
└─ Row 1 (Height=*) - Device List
   └─ ItemsControl (DevicesList)
      └─ Items bound to Devices collection
```

## Implementation

The skill will:

1. **Parse XAML structure:**
   - Identify container types
   - Extract positioning properties
   - Build a tree representation

2. **Check for issues:**
   - Grid without RowDefinitions/ColumnDefinitions but multiple children
   - Elements with same Row/Column (potential overlap)
   - Z-Index conflicts
   - Absolute positioning (Margin with specific values on all sides)
   - Missing Grid.Row/Grid.Column assignments

3. **Generate report:**
   ```
   ## XAML Analysis: FlyoutWindow.xaml (lines 115-230)
   
   ### Structure:
   [ASCII tree diagram]
   
   ### Issues Found:
   ⚠️ Line 127: Grid contains 2 elements without Row definitions
   ⚠️ Line 145: Button uses absolute Margin (0,0,42,2) - consider Grid columns
   ⚠️ Line 200: TextBlock might be obscured by Button (Z-Index issue)
   
   ### Recommendations:
   1. Add Grid.RowDefinitions to separate header and content
   2. Replace absolute Margin with Grid.ColumnDefinitions
   3. Set explicit Panel.ZIndex values for overlapping elements
   ```

4. **Offer to fix:**
   - Show proposed changes
   - Ask for confirmation
   - Apply fixes if approved

## Notes

- This skill is especially useful after making layout changes
- Can be used preventively before implementing new UI
- Helps maintain consistent layout patterns across the app
- Best used with Sonnet for accurate analysis
