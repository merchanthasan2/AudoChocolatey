from typing import List, Optional, Set, Dict, Any
import customtkinter as ctk
from core.models import PackageInfo, OperationStatus
from core.admin import is_admin
from ui.theme import THEME, FONTS
from ui.components import BentoCard, ProgressIsland, AppMonogram

class UpdatesTab(ctk.CTkFrame):
    """Tab 1: Glassmorphic Updates Hub with Bento Metrics, Monograms, and Progress Island."""

    def __init__(self, master, manager, on_log_callback, mode: str = "dark", **kwargs):
        super().__init__(master, fg_color="transparent", **kwargs)
        self.manager = manager
        self.on_log = on_log_callback
        self.mode = mode
        self.packages: List[PackageInfo] = []
        self.current_display_packages: List[PackageInfo] = []
        self.selected_ids: Set[str] = set()
        self.card_widgets: Dict[str, Dict[str, Any]] = {}

        # Main Scrollable View for the entire tab
        self.main_scroll = ctk.CTkScrollableFrame(
            self,
            fg_color="transparent",
            corner_radius=0
        )
        self.main_scroll.pack(fill="both", expand=True, padx=20, pady=(15, 10))

        # 1. BENTO OVERVIEW METRIC GRID
        self._build_bento_grid()

        # 2. DYNAMIC PROGRESS ISLAND
        self.progress_island = ProgressIsland(self.main_scroll, mode=self.mode)
        self.progress_island.pack(fill="x", pady=(0, 14))

        # 3. ACTION CONTROL BAR
        self._build_action_bar()

        # 4. PACKAGE CARDS CONTAINER
        self.cards_container = ctk.CTkFrame(self.main_scroll, fg_color="transparent")
        self.cards_container.pack(fill="both", expand=True)

        self.show_placeholder("Click 'Scan for Updates' to audit your installed applications.")

    def _build_bento_grid(self):
        theme = THEME[self.mode]
        self.bento_row = ctk.CTkFrame(self.main_scroll, fg_color="transparent")
        self.bento_row.pack(fill="x", pady=(0, 14))
        self.bento_row.grid_columnconfigure((0, 1, 2, 3), weight=1, uniform="bento")

        # Tile 1: Updates Available
        self.bento_updates = BentoCard(
            self.bento_row,
            title="Updates Available",
            value="0",
            subtitle="● Scanning needed",
            icon="🔄",
            accent_color=theme["warning"],
            mode=self.mode,
            on_click=self.start_scan
        )
        self.bento_updates.grid(row=0, column=0, padx=(0, 8), sticky="ew")

        # Tile 2: Total Installed Apps
        self.bento_installed = BentoCard(
            self.bento_row,
            title="Installed Apps",
            value="—",
            subtitle="● System software",
            icon="📦",
            accent_color=theme["primary"],
            mode=self.mode
        )
        self.bento_installed.grid(row=0, column=1, padx=4, sticky="ew")

        # Tile 3: Security & Elevation
        admin_active = is_admin()
        self.bento_security = BentoCard(
            self.bento_row,
            title="Security Context",
            value="Elevated" if admin_active else "Standard",
            subtitle="● Unattended admin" if admin_active else "● User mode",
            icon="🛡️",
            accent_color=theme["success"] if admin_active else theme["danger"],
            mode=self.mode
        )
        self.bento_security.grid(row=0, column=2, padx=4, sticky="ew")

        # Tile 4: Package Engine
        engine_name = self.manager.current_engine.name.split()[0]
        self.bento_engine = BentoCard(
            self.bento_row,
            title="Active Engine",
            value=engine_name,
            subtitle=f"● {self.manager.current_engine.get_version() or 'Active'}",
            icon="⚡",
            accent_color=theme["cyan"],
            mode=self.mode
        )
        self.bento_engine.grid(row=0, column=3, padx=(8, 0), sticky="ew")

    def _build_action_bar(self):
        theme = THEME[self.mode]
        self.action_bar = ctk.CTkFrame(
            self.main_scroll,
            fg_color=theme["bg_card"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.action_bar.pack(fill="x", pady=(0, 14))

        inner = ctk.CTkFrame(self.action_bar, fg_color="transparent")
        inner.pack(fill="x", padx=16, pady=10)

        # Master Checkbox for Select All
        self.cb_select_all = ctk.CTkCheckBox(
            inner,
            text="Select All",
            font=FONTS["body_bold"],
            width=22,
            height=22,
            corner_radius=6,
            fg_color=theme["primary"],
            command=self.on_select_all_toggle
        )
        self.cb_select_all.pack(side="left", padx=(0, 14))

        # Scan Button
        self.btn_check = ctk.CTkButton(
            inner,
            text="🔄 Scan for Updates",
            font=FONTS["body_bold"],
            fg_color=theme["primary"],
            hover_color=theme["primary_hover"],
            height=36,
            corner_radius=8,
            command=self.start_scan
        )
        self.btn_check.pack(side="left", padx=(0, 10))

        # Dynamic Update Action Button
        self.btn_update_action = ctk.CTkButton(
            inner,
            text="⚡ Update All Packages",
            font=FONTS["body_bold"],
            fg_color=theme["success"],
            hover_color=theme["success_hover"],
            height=36,
            corner_radius=8,
            state="disabled",
            command=self.start_update_action
        )
        self.btn_update_action.pack(side="left", padx=(0, 14))

        # Selection info label
        self.lbl_selection_info = ctk.CTkLabel(
            inner,
            text="",
            font=FONTS["small_bold"],
            text_color=theme["cyan"]
        )
        self.lbl_selection_info.pack(side="left", padx=4)

        # Search filter entry
        self.search_entry = ctk.CTkEntry(
            inner,
            placeholder_text="Filter pending updates...",
            font=FONTS["body"],
            height=36,
            fg_color=theme["bg_input"],
            border_color=theme["card_border"],
            corner_radius=8
        )
        self.search_entry.pack(side="right", fill="x", expand=True, padx=(10, 0))
        self.search_entry.bind("<KeyRelease>", self.on_search_filter)

    def show_placeholder(self, text: str):
        for widget in self.cards_container.winfo_children():
            widget.destroy()

        theme = THEME[self.mode]
        frame = ctk.CTkFrame(
            self.cards_container,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        frame.pack(fill="both", expand=True, pady=20)

        lbl = ctk.CTkLabel(
            frame,
            text=text,
            font=FONTS["subtitle"],
            text_color=theme["text_secondary"],
            wraplength=600
        )
        lbl.pack(padx=20, pady=50)

    def start_scan(self):
        if self.manager.is_busy:
            return

        self.btn_check.configure(state="disabled", text="Scanning...")
        self.btn_update_action.configure(state="disabled")
        self.cb_select_all.configure(state="disabled")
        self.selected_ids.clear()
        self.lbl_selection_info.configure(text="")
        self.progress_island.update_status(
            title="System Audit in Progress",
            ticker_msg="Analyzing installed applications and repository registries...",
            pct=None,
            is_active=True
        )
        self.show_placeholder("Querying package repositories and analyzing installed software...")

        def on_success(packages: List[PackageInfo]):
            def _update_ui():
                count = len(packages)
                self.progress_island.update_status(
                    title="Audit Complete",
                    ticker_msg=f"Found {count} available software updates ready to install.",
                    pct=1.0,
                    is_active=False
                )
                self.btn_check.configure(state="normal", text="🔄 Scan for Updates")
                self.cb_select_all.configure(state="normal")
                self.packages = packages
                self.current_display_packages = packages
                self.bento_updates.update_data(
                    value=str(count),
                    subtitle=f"● {count} ready to upgrade" if count > 0 else "● All apps up to date"
                )
                self.render_packages(self.packages)
                self.update_action_button()
            self.after(0, _update_ui)

        def on_error(err: Exception):
            def _err_ui():
                self.progress_island.update_status(
                    title="Scan Failed",
                    ticker_msg=str(err),
                    pct=0,
                    is_active=False
                )
                self.btn_check.configure(state="normal", text="🔄 Scan for Updates")
                self.cb_select_all.configure(state="disabled")
                self.show_placeholder(f"Scan failed: {str(err)}")
            self.after(0, _err_ui)

        self.manager.check_updates_async(
            on_log=self.on_log,
            on_success=on_success,
            on_error=on_error
        )

    def update_action_button(self):
        total_available = len(self.packages)
        num_selected = len(self.selected_ids)
        theme = THEME[self.mode]

        if total_available == 0:
            self.btn_update_action.configure(
                state="disabled",
                text="⚡ No Updates Available",
                fg_color=theme["badge_bg"],
                text_color=theme["text_muted"]
            )
            self.cb_select_all.deselect()
            self.cb_select_all.configure(state="disabled")
            self.lbl_selection_info.configure(text="")
            return

        self.cb_select_all.configure(state="normal")

        if num_selected == 0:
            self.btn_update_action.configure(
                state="normal",
                text=f"⚡ Update All ({total_available})",
                fg_color=theme["success"],
                hover_color=theme["success_hover"],
                text_color="#FFFFFF"
            )
            self.cb_select_all.deselect()
            self.lbl_selection_info.configure(text="")
        else:
            self.btn_update_action.configure(
                state="normal",
                text=f"⚡ Update Selected ({num_selected})",
                fg_color=theme["primary"],
                hover_color=theme["primary_hover"],
                text_color="#FFFFFF"
            )
            self.lbl_selection_info.configure(text=f"({num_selected} of {total_available} selected)")

            if num_selected == len(self.current_display_packages):
                self.cb_select_all.select()
            else:
                self.cb_select_all.deselect()

    def on_select_all_toggle(self):
        is_checked = bool(self.cb_select_all.get())
        if is_checked:
            for p in self.current_display_packages:
                self.selected_ids.add(p.id)
                if p.id in self.card_widgets:
                    self.card_widgets[p.id]["cb"].select()
        else:
            for p in self.current_display_packages:
                self.selected_ids.discard(p.id)
                if p.id in self.card_widgets:
                    self.card_widgets[p.id]["cb"].deselect()

        self.update_action_button()

    def on_item_checkbox_toggle(self, pkg_id: str, cb: ctk.CTkCheckBox):
        if bool(cb.get()):
            self.selected_ids.add(pkg_id)
        else:
            self.selected_ids.discard(pkg_id)

        self.update_action_button()

    def render_packages(self, packages: List[PackageInfo]):
        for widget in self.cards_container.winfo_children():
            widget.destroy()

        self.card_widgets.clear()
        self.current_display_packages = packages

        count = len(packages)
        if count == 0:
            self.show_placeholder("✨ All applications on your system are completely up to date!")
            self.update_action_button()
            return

        for pkg in packages:
            self._create_package_card(pkg)

        self.update_action_button()

    def _create_package_card(self, pkg: PackageInfo):
        theme = THEME[self.mode]

        # Glass Acrylic Card Container
        card = ctk.CTkFrame(
            self.cards_container,
            fg_color=theme["bg_card"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"]
        )
        card.pack(fill="x", pady=4)

        # Checkbox for selection
        cb = ctk.CTkCheckBox(
            card,
            text="",
            width=22,
            height=22,
            corner_radius=6,
            fg_color=theme["primary"],
            command=lambda pid=pkg.id: self.on_item_checkbox_toggle(pid, self.card_widgets[pid]["cb"])
        )
        cb.pack(side="left", padx=(14, 8), pady=14)
        if pkg.id in self.selected_ids:
            cb.select()

        # Visual Monogram Avatar
        monogram = AppMonogram(card, app_name=pkg.name, size=38)
        monogram.pack(side="left", padx=(0, 12), pady=12)

        # Info column
        info_frame = ctk.CTkFrame(card, fg_color="transparent")
        info_frame.pack(side="left", fill="both", expand=True, pady=12)

        # Top row: App Name + Source Badge + Status Pill
        title_row = ctk.CTkFrame(info_frame, fg_color="transparent")
        title_row.pack(fill="x")

        lbl_name = ctk.CTkLabel(
            title_row,
            text=pkg.name,
            font=FONTS["body_bold"],
            text_color=theme["text_primary"]
        )
        lbl_name.pack(side="left")

        # Source Badge
        is_winget = pkg.source.lower() == "winget"
        src_bg = theme["primary_glow"] if is_winget else theme["warning_dim"]
        src_fg = theme["primary"] if is_winget else theme["warning"]
        lbl_source = ctk.CTkLabel(
            title_row,
            text=pkg.source.upper(),
            font=FONTS["small_bold"],
            fg_color=src_bg,
            text_color=src_fg,
            corner_radius=6,
            padx=7,
            pady=2
        )
        lbl_source.pack(side="left", padx=8)

        # Live Status Pill
        lbl_status = ctk.CTkLabel(
            title_row,
            text="Ready",
            font=FONTS["small_bold"],
            fg_color=theme["badge_bg"],
            text_color=theme["text_muted"],
            corner_radius=6,
            padx=7,
            pady=2
        )
        lbl_status.pack(side="left", padx=4)

        # Meta row: ID + Version flow
        meta_row = ctk.CTkFrame(info_frame, fg_color="transparent")
        meta_row.pack(fill="x", pady=(3, 0))

        lbl_id = ctk.CTkLabel(
            meta_row,
            text=pkg.id,
            font=FONTS["small"],
            text_color=theme["text_muted"]
        )
        lbl_id.pack(side="left")

        # Version Flow Pill
        ver_text = f"v{pkg.installed_version}  ➔  v{pkg.available_version}"
        lbl_ver = ctk.CTkLabel(
            meta_row,
            text=ver_text,
            font=FONTS["small_bold"],
            fg_color=theme["bg_input"],
            text_color=theme["cyan"],
            corner_radius=6,
            padx=8,
            pady=2
        )
        lbl_ver.pack(side="left", padx=12)

        # Update button
        btn_update = ctk.CTkButton(
            card,
            text="Update",
            font=FONTS["small_bold"],
            fg_color=theme["primary"],
            hover_color=theme["primary_hover"],
            text_color="#FFFFFF",
            width=85,
            height=32,
            corner_radius=8
        )
        btn_update.configure(command=lambda p=pkg: self.upgrade_single(p))
        btn_update.pack(side="right", padx=16, pady=12)

        self.card_widgets[pkg.id] = {
            "card": card,
            "cb": cb,
            "btn": btn_update,
            "status_pill": lbl_status,
            "lbl_ver": lbl_ver,
            "pkg": pkg
        }

    def upgrade_single(self, pkg: PackageInfo):
        if self.manager.is_busy:
            return

        theme = THEME[self.mode]
        w = self.card_widgets.get(pkg.id)
        if w:
            w["btn"].configure(state="disabled", text="Updating...", fg_color=theme["warning"])
            w["status_pill"].configure(text="Starting...", fg_color=theme["warning_dim"], text_color=theme["warning"])

        self.progress_island.update_status(
            title=f"Upgrading: {pkg.name}",
            ticker_msg="Initializing unattended installer...",
            pct=None,
            is_active=True
        )

        def on_prog(pct_val: Optional[float], status_text: str):
            def _ui():
                self.progress_island.update_status(f"Upgrading: {pkg.name}", status_text, pct_val, True)
                if w:
                    pill_text = f"{int(pct_val * 100)}%" if pct_val is not None else "Installing..."
                    w["status_pill"].configure(text=pill_text, fg_color=theme["warning_dim"], text_color=theme["warning"])
            self.after(0, _ui)

        def on_success(res: OperationStatus):
            def _ui():
                if res.success:
                    self.progress_island.update_status("Upgrade Finished", f"Successfully upgraded {pkg.name}!", 1.0, False)
                    if w:
                        w["btn"].configure(text="✓ Updated", fg_color=theme["success"])
                        w["status_pill"].configure(text="✓ Done", fg_color=theme["success_dim"], text_color=theme["success"])
                    self.selected_ids.discard(pkg.id)
                    if w:
                        w["cb"].deselect()
                    self.update_action_button()
                else:
                    self.progress_island.update_status("Upgrade Failed", f"Failed with exit code {res.exit_code}.", 0, False)
                    if w:
                        w["btn"].configure(text="Failed", fg_color=theme["danger"], state="normal")
                        w["status_pill"].configure(text="Failed", fg_color=theme["danger_dim"], text_color=theme["danger"])
            self.after(0, _ui)

        def on_error(err: Exception):
            def _ui():
                self.progress_island.update_status("Upgrade Error", str(err), 0, False)
                if w:
                    w["btn"].configure(text="Error", fg_color=theme["danger"], state="normal")
                    w["status_pill"].configure(text="Error", fg_color=theme["danger_dim"], text_color=theme["danger"])
            self.after(0, _ui)

        self.manager.upgrade_package_async(
            package_id=pkg.id,
            on_log=self.on_log,
            on_progress=on_prog,
            on_success=on_success,
            on_error=on_error
        )

    def start_update_action(self):
        if self.manager.is_busy:
            return

        if len(self.selected_ids) > 0:
            target_ids = [p.id for p in self.packages if p.id in self.selected_ids]
        else:
            target_ids = [p.id for p in self.packages]

        if not target_ids:
            return

        theme = THEME[self.mode]
        self.btn_update_action.configure(state="disabled", text=f"Updating {len(target_ids)}...", fg_color=theme["badge_bg"])
        self.btn_check.configure(state="disabled")
        self.cb_select_all.configure(state="disabled")

        total_count = len(target_ids)

        def on_item_start(pkg_id: str, current_idx: int, total: int):
            def _ui():
                w = self.card_widgets.get(pkg_id)
                pkg_name = w["pkg"].name if w else pkg_id
                self.progress_island.update_status(
                    f"[{current_idx}/{total}] Upgrading: {pkg_name}",
                    "Preparing download...",
                    0.0,
                    True
                )
                if w:
                    w["btn"].configure(state="disabled", text="Updating...", fg_color=theme["warning"])
                    w["status_pill"].configure(text="Active", fg_color=theme["warning_dim"], text_color=theme["warning"])
            self.after(0, _ui)

        def on_item_progress(pkg_id: str, pct_val: Optional[float], status_text: str):
            def _ui():
                w = self.card_widgets.get(pkg_id)
                pkg_name = w["pkg"].name if w else pkg_id
                self.progress_island.update_status(
                    f"Upgrading: {pkg_name}",
                    status_text,
                    pct_val,
                    True
                )
                if w and pct_val is not None:
                    w["status_pill"].configure(text=f"{int(pct_val * 100)}%", fg_color=theme["warning_dim"], text_color=theme["warning"])
            self.after(0, _ui)

        def on_item_done(pkg_id: str, status: OperationStatus, current_idx: int, total: int):
            def _ui():
                w = self.card_widgets.get(pkg_id)
                if w:
                    if status.success:
                        w["btn"].configure(text="✓ Updated", fg_color=theme["success"])
                        w["status_pill"].configure(text="✓ Done", fg_color=theme["success_dim"], text_color=theme["success"])
                        self.selected_ids.discard(pkg_id)
                        w["cb"].deselect()
                    else:
                        w["btn"].configure(text="Failed", fg_color=theme["danger"], state="normal")
                        w["status_pill"].configure(text="Failed", fg_color=theme["danger_dim"], text_color=theme["danger"])
            self.after(0, _ui)

        def on_complete(results: List[OperationStatus]):
            def _ui():
                successes = sum(1 for r in results if r.success)
                self.progress_island.update_status(
                    "Batch Finished",
                    f"Completed {successes}/{total_count} upgrades successfully.",
                    1.0,
                    False
                )
                self.btn_check.configure(state="normal")
                self.cb_select_all.configure(state="normal")
                self.update_action_button()
                self.start_scan()
            self.after(0, _ui)

        def on_error(err: Exception):
            def _ui():
                self.progress_island.update_status("Batch Error", str(err), 0, False)
                self.btn_check.configure(state="normal")
                self.cb_select_all.configure(state="normal")
                self.update_action_button()
            self.after(0, _ui)

        self.manager.upgrade_multiple_async(
            package_ids=target_ids,
            on_log=self.on_log,
            on_item_start=on_item_start,
            on_item_progress=on_item_progress,
            on_item_done=on_item_done,
            on_complete=on_complete,
            on_error=on_error
        )

    def on_search_filter(self, event=None):
        query = self.search_entry.get().strip().lower()
        if not query:
            self.render_packages(self.packages)
            return

        filtered = [
            p for p in self.packages 
            if query in p.name.lower() or query in p.id.lower()
        ]
        self.render_packages(filtered)

    def set_theme_mode(self, mode: str):
        """Dynamic theme refresh."""
        self.mode = mode
        theme = THEME[mode]

        self.bento_updates.set_theme_mode(mode)
        self.bento_installed.set_theme_mode(mode)
        self.bento_security.set_theme_mode(mode)
        self.bento_engine.set_theme_mode(mode)
        self.progress_island.set_theme_mode(mode)

        self.action_bar.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.btn_check.configure(fg_color=theme["primary"], hover_color=theme["primary_hover"])
        self.search_entry.configure(fg_color=theme["bg_input"], border_color=theme["card_border"])
        self.cb_select_all.configure(fg_color=theme["primary"])

        self.render_packages(self.current_display_packages)
