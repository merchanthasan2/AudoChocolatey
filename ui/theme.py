"""Modern Glassmorphism Theme System & Color Tokens."""

THEME = {
    "dark": {
        # Atmospheric Backgrounds
        "bg_main": "#090D16",           # Deep obsidian canvas
        "bg_surface": "#0E1524",        # Header & navigation glass surface
        "bg_card": "#131B2E",           # Translucent acrylic card body
        "bg_card_hover": "#1A253D",     # Elevated card hover state
        "bg_card_active": "#1E2C48",    # Selected or active card
        "bg_input": "#0B111D",          # Recessed search / input wells
        "card_border": "#243354",       # 1px subtle glass edge highlight
        "card_border_hover": "#3B527E", # Border glow on hover

        # Typography
        "text_primary": "#F8FAFC",      # Crisp high contrast white
        "text_secondary": "#94A3B8",    # Balanced cool slate
        "text_muted": "#64748B",        # Dimmed subtext & metadata
        "text_accent": "#818CF8",       # Indigo highlight text

        # Vibrant Accent System
        "primary": "#6366F1",           # Electric Indigo
        "primary_hover": "#4F46E5",
        "primary_glow": "#312E81",
        "cyan": "#06B6D4",              # Luminous Cyan (Download & active progress)
        "cyan_hover": "#0891B2",
        "cyan_dim": "#164E63",
        "success": "#10B981",           # Mint Emerald
        "success_hover": "#059669",
        "success_dim": "#064E3B",
        "warning": "#F59E0B",           # Warm Amber (Pending updates)
        "warning_hover": "#D97706",
        "warning_dim": "#78350F",
        "danger": "#F43F5E",            # Coral Rose
        "danger_hover": "#E11D48",
        "danger_dim": "#4C0519",

        # Console & Terminal
        "terminal_bg": "#060A12",
        "terminal_text": "#38BDF8",

        # Badges & Pills
        "badge_bg": "#1E293B",
        "badge_text": "#CBD5E1",
        "badge_border": "#334155",
        "bg_secondary": "#0F172A",
        "accent": "#38BDF8",
        "accent_glow": "#164E63",
        "danger_glow": "#4C0519",
        "success_glow": "#064E3B",
        "island_bg": "#0D1424",
        "island_border": "#28385C"
    },
    "light": {
        # Atmospheric Backgrounds
        "bg_main": "#F1F5F9",           # Pearl slate canvas
        "bg_surface": "#FFFFFF",        # Pure acrylic header/sidebar
        "bg_card": "#FFFFFF",           # Clean elevated card
        "bg_card_hover": "#F8FAFC",     # Subtle hover lift
        "bg_card_active": "#EEF2F6",    # Active card state
        "bg_input": "#F8FAFC",          # Soft input background
        "card_border": "#E2E8F0",       # Clean light border
        "card_border_hover": "#CBD5E1",

        # Typography
        "text_primary": "#0F172A",      # Deep charcoal slate
        "text_secondary": "#475569",    # Balanced dark graphite
        "text_muted": "#64748B",        # Muted neutral
        "text_accent": "#4F46E5",       # Royal indigo

        # Vibrant Accent System
        "primary": "#4F46E5",           # Royal Indigo
        "primary_hover": "#4338CA",
        "primary_glow": "#C7D2FE",
        "cyan": "#0284C7",              # Sky Cobalt
        "cyan_hover": "#0369A1",
        "cyan_dim": "#E0F2FE",
        "success": "#059669",           # Forest Mint
        "success_hover": "#047857",
        "success_dim": "#D1FAE5",
        "warning": "#D97706",           # Sunset Ochre
        "warning_hover": "#B45309",
        "warning_dim": "#FEF3C7",
        "danger": "#E11D48",            # Crimson Rose
        "danger_hover": "#BE123C",
        "danger_dim": "#FFE4E6",

        # Console & Terminal
        "terminal_bg": "#1E293B",
        "terminal_text": "#67E8F9",

        # Badges & Pills
        "badge_bg": "#E2E8F0",
        "badge_text": "#334155",
        "badge_border": "#CBD5E1",
        "bg_secondary": "#F8FAFC",
        "accent": "#0284C7",
        "accent_glow": "#E0F2FE",
        "danger_glow": "#FFE4E6",
        "success_glow": "#D1FAE5",
        "island_bg": "#FFFFFF",
        "island_border": "#E2E8F0"
    }
}

FONTS = {
    "display": ("Segoe UI Variable Display", 22, "bold"),
    "title": ("Segoe UI Variable Display", 18, "bold"),
    "subtitle": ("Segoe UI Variable Text", 14, "bold"),
    "body": ("Segoe UI Variable Text", 12),
    "body_bold": ("Segoe UI Variable Text", 12, "bold"),
    "small": ("Segoe UI Variable Text", 10),
    "small_bold": ("Segoe UI Variable Text", 10, "bold"),
    "mono": ("Consolas", 10),
    "mono_bold": ("Consolas", 10, "bold")
}

# Curated gradient palettes for dynamic app monograms
MONOGRAM_PALETTES = [
    ("#312E81", "#818CF8"),  # Indigo
    ("#164E63", "#38BDF8"),  # Cyan
    ("#064E3B", "#34D399"),  # Emerald
    ("#78350F", "#FBBF24"),  # Amber
    ("#581C87", "#C084FC"),  # Purple
    ("#831843", "#F472B6"),  # Pink
    ("#1E293B", "#94A3B8")   # Slate
]

def get_monogram_colors(name: str):
    """Deterministically choose a rich accent pair for an app monogram."""
    hash_val = sum(ord(c) for c in (name or "App"))
    bg_color, text_color = MONOGRAM_PALETTES[hash_val % len(MONOGRAM_PALETTES)]
    return bg_color, text_color
