

## Chrome-style top tabs and native Windows caption buttons

The main window uses WPF `WindowChrome` with a custom non-client frame. Tabs and the new-tab (+) button occupy the top caption area, while Windows draws the native minimize, maximize/restore, and close buttons at the top-right. The right side of the tab strip reserves space so tabs do not overlap the native buttons.
