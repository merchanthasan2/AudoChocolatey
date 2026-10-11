from dataclasses import dataclass, field
from typing import Optional, List, Dict, Any

@dataclass
class PackageInfo:
    """Represents a software package with installed and available versions."""
    name: str
    id: str
    installed_version: str
    available_version: str
    source: str = "unknown"
    engine: str = "winget"  # 'winget' or 'choco'
    description: str = ""
    is_pinned: bool = False
    explicit_upgrade_required: bool = False
    
    @property
    def has_update(self) -> bool:
        if not self.available_version:
            return False
        return self.installed_version != self.available_version

@dataclass
class SearchResult:
    """Represents a search result from a package repository."""
    name: str
    id: str
    version: str
    source: str
    engine: str
    match_tag: str = ""
    description: str = ""

@dataclass
class OperationStatus:
    """Status of an install or upgrade operation."""
    package_id: str
    success: bool
    message: str
    exit_code: int = 0
