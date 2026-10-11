from typing import List, Optional
import customtkinter as ctk
from core.models import SearchResult
from ui.theme import THEME, FONTS
from ui.components import AppMonogram

QUICK_TAGS = [
    ("VS Code", "Microsoft.VisualStudioCode"),
    ("Google Chrome", "Google.Chrome"),
    ("Mozilla Firefox", "Mozilla.Firefox"),
    ("VLC Player", "VideoLAN.VLC"),
    ("Git", "Git.Git"),
    ("7-Zip", "7zip.7zip"),
    ("Discord", "Discord.Discord"),
    ("Notepad++", "Notepad++.Notepad++"),
    ("Python", "Python.Python.3.13"),
    ("Docker", "Docker.DockerDesktop")
]

class ExploreTab(ctk.CTkFrame):
    """Tab 2: Glassmorphic Software Catalog & Discovery (Phase 2)."""

    def __init__(self, master, manager, on_log_callback, mode: str = "dark", **kwargs):
        super().__init__(master, fg_color="transparent", **kwargs)
        self.manager = manager
        self.on_log = on_log_callback
        self.mode = mode
        self.results: List[SearchResult] = []

        # Main Scrollable Container
        self.main_scroll = ctk.CTkScrollableFrame(
            self,
            fg_color="transparent",
            corner_radius=0
        )
        self.main_scroll.pack(fill="both", expand=True, padx=20, pady=(15, 10))

        # Top Search Card
        self._build_search_card()

        # Results Container
        self.results_container = ctk.CTkFrame(self.main_scroll, fg_color="transparent")
        self.results_container.pack(fill="both", expand=True)

        self.show_placeholder("Type an application name above or pick a popular tool to search the repository.")

    def _build_search_card(self):
        theme = THEME[self.mode]

        self.search_card = ctk.CTkFrame(
            self.main_scroll,
            fg_color=theme["bg_card"],
            corner_radius=14,
            border_width=1,
            border_color=theme["card_border"]
        )
        self.search_card.pack(fill="x", pady=(0, 16))

        inner = ctk.CTkFrame(self.search_card, fg_color="transparent")
        inner.pack(fill="x", padx=18, pady=16)

        # Header Titles
        self.lbl_title = ctk.CTkLabel(
            inner,
            text="Explore & Discover Software",
            font=FONTS["title"],
            text_color=theme["text_primary"]
        )
        self.lbl_title.pack(anchor="w")

        self.lbl_sub = ctk.CTkLabel(
            inner,
            text="Search the global Windows repository to find, audit, and install verified applications.",
            font=FONTS["small"],
            text_color=theme["text_muted"]
        )
        self.lbl_sub.pack(anchor="w", pady=(2, 12))

        # Search Bar Row
        self.search_row = ctk.CTkFrame(inner, fg_color="transparent")
        self.search_row.pack(fill="x", pady=(0, 12))

        self.entry_search = ctk.CTkEntry(
            self.search_row,
            placeholder_text="Search by name, publisher, or ID (e.g. 'blender', 'obs', 'powertoys')...",
            font=FONTS["body"],
            height=40,
            fg_color=theme["bg_input"],
            border_color=theme["card_border"],
            corner_radius=10
        )
        self.entry_search.pack(side="left", fill="x", expand=True, padx=(0, 10))
        self.entry_search.bind("<Return>", lambda e: self.perform_search())

        self.btn_search = ctk.CTkButton(
            self.search_row,
            text="🔍 Search Store",
            font=FONTS["body_bold"],
            fg_color=theme["primary"],
            hover_color=theme["primary_hover"],
            height=40,
            width=130,
            corner_radius=10,
            command=self.perform_search
        )
        self.btn_search.pack(side="right")

        # Popular Tags Row
        self.tags_frame = ctk.CTkFrame(inner, fg_color="transparent")
        self.tags_frame.pack(fill="x")

        lbl_popular = ctk.CTkLabel(
            self.tags_frame,
            text="Quick Picks:",
            font=FONTS["small_bold"],
            text_color=theme["text_secondary"]
        )
        lbl_popular.pack(side="left", padx=(0, 8))

        for name, query in QUICK_TAGS[:7]:
            btn_tag = ctk.CTkButton(
                self.tags_frame,
                text=name,
                font=FONTS["small"],
                height=26,
                fg_color=theme["bg_input"],
                hover_color=theme["bg_card_hover"],
                text_color=theme["text_secondary"],
                corner_radius=13,
                border_width=1,
                border_color=theme["card_border"],
                command=lambda q=name: self.search_tag(q)
            )
            btn_tag.pack(side="left", padx=3)

    def search_tag(self, query: str):
        self.entry_search.delete(0, "end")
        self.entry_search.insert(0, query)
        self.perform_search()

    def show_placeholder(self, text: str):
        for widget in self.results_container.winfo_children():
            widget.destroy()

        theme = THEME[self.mode]
        frame = ctk.CTkFrame(
            self.results_container,
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

    def perform_search(self):
        query = self.entry_search.get().strip()
        if not query:
            return

        if self.manager.is_busy:
            return

        self.btn_search.configure(state="disabled", text="Searching...")
        self.show_placeholder(f"Searching global package repository for '{query}'...")

        def on_success(results: List[SearchResult]):
            def _ui():
                self.btn_search.configure(state="normal", text="🔍 Search Store")
                self.results = results
                self.render_results(results)
            self.after(0, _ui)

        def on_error(err: Exception):
            def _ui():
                self.btn_search.configure(state="normal", text="🔍 Search Store")
                self.show_placeholder(f"Search failed: {str(err)}")
            self.after(0, _ui)

        self.manager.search_packages_async(
            query=query,
            on_log=self.on_log,
            on_success=on_success,
            on_error=on_error
        )

    def render_results(self, results: List[SearchResult]):
        for widget in self.results_container.winfo_children():
            widget.destroy()

        if not results:
            self.show_placeholder("No matching packages found in repository. Try searching for a different keyword.")
            return

        for item in results:
            self._create_result_card(item)

    def _create_result_card(self, item: SearchResult):
        theme = THEME[self.mode]

        card = ctk.CTkFrame(
            self.results_container,
            fg_color=theme["bg_card"],
            corner_radius=12,
            border_width=1,
            border_color=theme["card_border"]
        )
        card.pack(fill="x", pady=4)

        # Monogram Icon
        monogram = AppMonogram(card, app_name=item.name, size=38)
        monogram.pack(side="left", padx=(14, 12), pady=12)

        info_frame = ctk.CTkFrame(card, fg_color="transparent")
        info_frame.pack(side="left", fill="both", expand=True, pady=12)

        # Title row
        title_row = ctk.CTkFrame(info_frame, fg_color="transparent")
        title_row.pack(fill="x")

        lbl_name = ctk.CTkLabel(
            title_row,
            text=item.name,
            font=FONTS["body_bold"],
            text_color=theme["text_primary"]
        )
        lbl_name.pack(side="left")

        # Source badge
        is_winget = item.source.lower() == "winget"
        src_bg = theme["primary_glow"] if is_winget else theme["warning_dim"]
        src_fg = theme["primary"] if is_winget else theme["warning"]
        lbl_source = ctk.CTkLabel(
            title_row,
            text=item.source.upper(),
            font=FONTS["small_bold"],
            fg_color=src_bg,
            text_color=src_fg,
            corner_radius=6,
            padx=7,
            pady=2
        )
        lbl_source.pack(side="left", padx=8)

        # Meta row
        meta_row = ctk.CTkFrame(info_frame, fg_color="transparent")
        meta_row.pack(fill="x", pady=(3, 0))

        lbl_id = ctk.CTkLabel(
            meta_row,
            text=f"ID: {item.id}",
            font=FONTS["small"],
            text_color=theme["text_muted"]
        )
        lbl_id.pack(side="left")

        if item.version:
            lbl_ver = ctk.CTkLabel(
                meta_row,
                text=f"Latest: v{item.version}",
                font=FONTS["small_bold"],
                fg_color=theme["bg_input"],
                text_color=theme["cyan"],
                corner_radius=6,
                padx=8,
                pady=2
            )
            lbl_ver.pack(side="left", padx=12)

        # Install button
        btn_install = ctk.CTkButton(
            card,
            text="Install",
            font=FONTS["small_bold"],
            fg_color=theme["success"],
            hover_color=theme["success_hover"],
            text_color="#FFFFFF",
            width=85,
            height=32,
            corner_radius=8
        )
        btn_install.configure(command=lambda res=item, b=btn_install: self.install_package(res, b))
        btn_install.pack(side="right", padx=16, pady=12)

    def install_package(self, item: SearchResult, btn: ctk.CTkButton):
        if self.manager.is_busy:
            return

        theme = THEME[self.mode]
        btn.configure(state="disabled", text="Starting...", fg_color=theme["warning"])

        def on_prog(pct_val: Optional[float], status_text: str):
            def _ui():
                if pct_val is not None:
                    btn.configure(text=f"{int(pct_val * 100)}%")
                else:
                    btn.configure(text="Installing...")
            self.after(0, _ui)

        def on_success(res):
            def _ui():
                if res.success:
                    btn.configure(text="✓ Installed", fg_color=theme["success"])
                else:
                    btn.configure(text="Retry", fg_color=theme["danger"], state="normal")
            self.after(0, _ui)

        def on_error(err):
            def _ui():
                btn.configure(text="Error", fg_color=theme["danger"], state="normal")
            self.after(0, _ui)

        self.manager.install_package_async(
            package_id=item.id,
            on_log=self.on_log,
            on_progress=on_prog,
            on_success=on_success,
            on_error=on_error
        )

    def set_theme_mode(self, mode: str):
        self.mode = mode
        theme = THEME[mode]
        self.search_card.configure(fg_color=theme["bg_card"], border_color=theme["card_border"])
        self.lbl_title.configure(text_color=theme["text_primary"])
        self.lbl_sub.configure(text_color=theme["text_muted"])
        self.entry_search.configure(fg_color=theme["bg_input"], border_color=theme["card_border"])
        self.btn_search.configure(fg_color=theme["primary"], hover_color=theme["primary_hover"])
        self.render_results(self.results)
