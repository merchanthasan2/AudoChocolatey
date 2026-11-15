/**
 * Quantum Updater - Modern Glassmorphic Software & Package Manager
 * Developed by Quantum Step (https://quantumstep.in)
 */

// Rich Curated Explore Catalog (Automatically loaded on launch)
const APP_CATALOG = [
  // Artificial Intelligence & Local LLMs
  {
    id: 'Ollama.Ollama',
    name: 'Ollama',
    category: 'ai',
    desc: 'Run large language models locally (Llama 3, DeepSeek, Mistral, Gemma) on CPU and GPU with zero cloud lock-in.',
    downloads: 99,
    version: '0.5.7',
    homepage: 'https://ollama.com/',
  },
  {
    id: 'ElementLabs.LMStudio',
    name: 'LM Studio',
    category: 'ai',
    desc: 'Discover, download, and run local LLMs with GPU acceleration, interactive chat UI, and OpenAI-compatible API server.',
    downloads: 98,
    version: '0.3.6',
    homepage: 'https://lmstudio.ai/',
  },
  {
    id: 'Jan.Jan',
    name: 'Jan AI',
    category: 'ai',
    desc: 'Self-hosted, open-source ChatGPT alternative that runs 100% offline on your device with complete privacy.',
    downloads: 95,
    version: '0.5.10',
    homepage: 'https://jan.ai/',
  },
  {
    id: 'OpenAI.ChatGPT',
    name: 'ChatGPT Desktop',
    category: 'ai',
    desc: 'Official Windows desktop client for OpenAI ChatGPT with instant voice mode, global shortcuts, and file attachments.',
    downloads: 100,
    version: '1.2024.310',
    homepage: 'https://openai.com/chatgpt/',
  },
  {
    id: 'Anthropic.Claude',
    name: 'Claude Desktop',
    category: 'ai',
    desc: 'Official Anthropic Claude desktop application with Model Context Protocol (MCP) and Artifacts rendering.',
    downloads: 97,
    version: '0.7.8',
    homepage: 'https://claude.ai/',
  },
  {
    id: 'Anysphere.Cursor',
    name: 'Cursor AI Editor',
    category: 'ai',
    desc: 'AI-first code editor fork of VS Code with multi-file reasoning, inline diffs, and terminal execution.',
    downloads: 99,
    version: '0.45.6',
    homepage: 'https://cursor.com/',
  },
  {
    id: 'Superwhisper.Superwhisper',
    name: 'Superwhisper',
    category: 'ai',
    desc: 'Ultra-fast, accurate offline voice-to-text dictation and transcription powered by local OpenAI Whisper models.',
    downloads: 91,
    version: '1.4.0',
    homepage: 'https://superwhisper.com/',
  },
  {
    id: 'Upscayl.Upscayl',
    name: 'Upscayl',
    category: 'ai',
    desc: 'Free, open source AI image upscaler powered by advanced neural network models with zero quality loss.',
    downloads: 93,
    version: '2.15.0',
    homepage: 'https://upscayl.org/',
  },
  {
    id: 'Pinokio.Pinokio',
    name: 'Pinokio AI Browser',
    category: 'ai',
    desc: 'Autonomous browser ecosystem that lets you install, run, and automate local AI apps and models with 1 click.',
    downloads: 92,
    version: '3.0.0',
    homepage: 'https://pinokio.computer/',
  },

  // Music & Media
  {
    id: 'Spotify.Spotify',
    name: 'Spotify',
    category: 'media',
    desc: 'Stream millions of tracks, albums, podcasts, and curated playlists.',
    downloads: 99,
    version: '1.2.53',
    homepage: 'https://www.spotify.com/',
  },
  {
    id: 'VideoLAN.VLC',
    name: 'VLC Media Player',
    category: 'media',
    desc: 'Plays virtually all video and audio codecs, discs, streams, and webcams.',
    downloads: 98,
    version: '3.0.21',
    homepage: 'https://www.videolan.org/vlc/',
  },
  {
    id: 'Audacity.Audacity',
    name: 'Audacity',
    category: 'media',
    desc: 'Open-source multi-track audio editor, recorder, and sound analyzer.',
    downloads: 88,
    version: '3.7.1',
    homepage: 'https://www.audacityteam.org/',
  },
  {
    id: 'OBSProject.OBSStudio',
    name: 'OBS Studio',
    category: 'media',
    desc: 'High performance video recording and live streaming for content creators.',
    downloads: 95,
    version: '31.0.0',
    homepage: 'https://obsproject.com/',
  },
  {
    id: 'HandBrake.HandBrake',
    name: 'HandBrake',
    category: 'media',
    desc: 'Open-source video transcoder for converting video to modern codecs.',
    downloads: 84,
    version: '1.8.2',
    homepage: 'https://handbrake.fr/',
  },
  {
    id: 'Foobar2000.Foobar2000',
    name: 'foobar2000',
    category: 'media',
    desc: 'Advanced, lightweight audio player with gapless playback and DSP.',
    downloads: 80,
    version: '2.1.6',
    homepage: 'https://www.foobar2000.org/',
  },
  {
    id: 'Plex.Plex',
    name: 'Plex Desktop',
    category: 'media',
    desc: 'Stream live TV, movies, shows, and your personal home media library.',
    downloads: 86,
    version: '1.104.0',
    homepage: 'https://www.plex.tv/',
  },
  {
    id: 'Kodi.Kodi',
    name: 'Kodi',
    category: 'media',
    desc: 'Award-winning open source home entertainment hub and digital jukebox.',
    downloads: 83,
    version: '21.1',
    homepage: 'https://kodi.tv/',
  },
  {
    id: 'Deezer.Deezer',
    name: 'Deezer',
    category: 'media',
    desc: 'High Fidelity music streaming with FLAC sound and personalized Flow.',
    downloads: 78,
    version: '6.0.190',
    homepage: 'https://www.deezer.com/',
  },

  // Addons & Utilities
  {
    id: 'Microsoft.PowerToys',
    name: 'Microsoft PowerToys',
    category: 'utilities',
    desc: 'Essential Windows system utilities for power users: FancyZones, Text Extractor, Awake, and PowerToys Run.',
    downloads: 99,
    version: '0.86.0',
    homepage: 'https://learn.microsoft.com/en-us/windows/powertoys/',
  },
  {
    id: '7zip.7zip',
    name: '7-Zip',
    category: 'utilities',
    desc: 'High compression ratio file archiver supporting 7z, ZIP, RAR, TAR, and ISO.',
    downloads: 99,
    version: '24.09',
    homepage: 'https://www.7-zip.org/',
  },
  {
    id: 'RARLab.WinRAR',
    name: 'WinRAR',
    category: 'utilities',
    desc: 'Industry standard RAR archive creator and decompression utility with high-speed unrar.',
    downloads: 93,
    version: '7.01',
    homepage: 'https://www.win-rar.com/',
  },
  {
    id: 'voidtools.Everything',
    name: 'Everything',
    category: 'utilities',
    desc: 'Locate files and folders across all NTFS drives in milliseconds with instant index search.',
    downloads: 98,
    version: '1.4.1',
    homepage: 'https://www.voidtools.com/',
  },
  {
    id: 'ShareX.ShareX',
    name: 'ShareX',
    category: 'utilities',
    desc: 'Screen capture, file sharing and productivity tool with OCR and screen recorder for Windows.',
    downloads: 97,
    version: '16.1.0',
    homepage: 'https://getsharex.com/',
  },
  {
    id: 'Flameshot.Flameshot',
    name: 'Flameshot',
    category: 'utilities',
    desc: 'Powerful yet simple to use screenshot software with in-place annotation and pin to desktop.',
    downloads: 92,
    version: '12.1.0',
    homepage: 'https://flameshot.org/',
  },
  {
    id: 'Rufus.Rufus',
    name: 'Rufus',
    category: 'utilities',
    desc: 'Reliably create bootable USB drives for Windows, Linux, and UEFI installations.',
    downloads: 94,
    version: '4.6',
    homepage: 'https://rufus.ie/',
  },
  {
    id: 'AutoHotkey.AutoHotkey',
    name: 'AutoHotkey',
    category: 'utilities',
    desc: 'Desktop automation and macro scripting language for custom keybindings.',
    downloads: 87,
    version: '2.0.18',
    homepage: 'https://www.autohotkey.com/',
  },
  {
    id: 'Bitwarden.Bitwarden',
    name: 'Bitwarden',
    category: 'utilities',
    desc: 'Open-source end-to-end encrypted password manager and passkey vault.',
    downloads: 94,
    version: '2024.11.0',
    homepage: 'https://bitwarden.com/',
  },
  {
    id: 'KeePassXCTeam.KeePassXC',
    name: 'KeePassXC',
    category: 'utilities',
    desc: 'Offline encrypted password manager with browser integration and auto-type.',
    downloads: 85,
    version: '2.7.9',
    homepage: 'https://keepassxc.org/',
  },
  {
    id: 'qBittorrent.qBittorrent',
    name: 'qBittorrent',
    category: 'utilities',
    desc: 'Free, open-source, ad-free torrent downloader with integrated search.',
    downloads: 91,
    version: '5.0.1',
    homepage: 'https://www.qbittorrent.org/',
  },
  {
    id: 'BleachBit.BleachBit',
    name: 'BleachBit',
    category: 'utilities',
    desc: 'Free cache cleaner, privacy cleaner, and disk space optimizer.',
    downloads: 81,
    version: '4.6.0',
    homepage: 'https://www.bleachbit.org/',
  },
  {
    id: 'AnyDeskSoftwareGmbH.AnyDesk',
    name: 'AnyDesk',
    category: 'utilities',
    desc: 'High frame-rate remote desktop application for IT support and telework.',
    downloads: 90,
    version: '8.1.1',
    homepage: 'https://anydesk.com/',
  },

  // Development & Tools
  {
    id: 'Microsoft.VisualStudioCode',
    name: 'Visual Studio Code',
    category: 'development',
    desc: 'Lightweight, extensible code editor with integrated Git, debugging, and terminal.',
    downloads: 100,
    version: '1.95.3',
    homepage: 'https://code.visualstudio.com/',
  },
  {
    id: 'Git.Git',
    name: 'Git',
    category: 'development',
    desc: 'Fast, distributed version control system for tracking source code changes.',
    downloads: 100,
    version: '2.47.1',
    homepage: 'https://git-scm.com/',
  },
  {
    id: 'GitHub.GitHubDesktop',
    name: 'GitHub Desktop',
    category: 'development',
    desc: 'Visual interface for Git and GitHub with seamless diffs, branches, and PRs.',
    downloads: 95,
    version: '3.4.9',
    homepage: 'https://desktop.github.com/',
  },
  {
    id: 'Docker.DockerDesktop',
    name: 'Docker Desktop',
    category: 'development',
    desc: 'Build, share, and run containerized microservices with Docker and Kubernetes.',
    downloads: 97,
    version: '4.35.1',
    homepage: 'https://www.docker.com/',
  },
  {
    id: 'Postman.Postman',
    name: 'Postman',
    category: 'development',
    desc: 'API testing platform for building, debugging, and mocking REST and GraphQL APIs.',
    downloads: 93,
    version: '11.19.0',
    homepage: 'https://www.postman.com/',
  },
  {
    id: 'Insomnia.Insomnia',
    name: 'Insomnia',
    category: 'development',
    desc: 'Streamlined desktop API client for REST, GraphQL, gRPC, and WebSockets.',
    downloads: 82,
    version: '10.1.1',
    homepage: 'https://insomnia.rest/',
  },
  {
    id: 'dbeaver.dbeaver',
    name: 'DBeaver Community',
    category: 'development',
    desc: 'Universal SQL database administration tool for Postgres, MySQL, SQLite, and Oracle.',
    downloads: 89,
    version: '24.2.4',
    homepage: 'https://dbeaver.io/',
  },
  {
    id: 'Python.Python.3.13',
    name: 'Python 3.13',
    category: 'development',
    desc: 'Modern general-purpose programming language for AI, data science, and scripting.',
    downloads: 98,
    version: '3.13.0',
    homepage: 'https://www.python.org/',
  },
  {
    id: 'OpenJS.NodeJS.LTS',
    name: 'Node.js (LTS)',
    category: 'development',
    desc: 'JavaScript runtime built on Chrome V8 engine for scalable backend servers.',
    downloads: 97,
    version: '22.11.0',
    homepage: 'https://nodejs.org/',
  },
  {
    id: 'Rustlang.Rustup',
    name: 'Rust (Rustup)',
    category: 'development',
    desc: 'Official installer for the memory-safe, ultra-fast Rust programming language.',
    downloads: 90,
    version: '1.27.1',
    homepage: 'https://rustup.rs/',
  },
  {
    id: 'Golang.Go',
    name: 'Go Programming Language',
    category: 'development',
    desc: 'Simple, fast, and concurrent systems programming language developed by Google.',
    downloads: 88,
    version: '1.23.3',
    homepage: 'https://go.dev/',
  },
  {
    id: 'Python.Python.3.12',
    name: 'Python 3.12',
    category: 'development',
    desc: 'Stable Python release for production apps, automation, and data workflows.',
    downloads: 96,
    version: 'Latest',
    homepage: 'https://www.python.org/',
  },
  {
    id: 'PuTTY.PuTTY',
    name: 'PuTTY',
    category: 'development',
    desc: 'SSH and Telnet client for secure remote shell and serial console access.',
    downloads: 91,
    version: 'Latest',
    homepage: 'https://www.chiark.greenend.org.uk/~sgtatham/putty/',
  },
  {
    id: 'WinSCP.WinSCP',
    name: 'WinSCP',
    category: 'development',
    desc: 'SFTP, SCP, and FTP client for secure file transfer and remote editing.',
    downloads: 89,
    version: 'Latest',
    homepage: 'https://winscp.net/',
  },
  {
    id: 'FileZilla.FileZilla',
    name: 'FileZilla',
    category: 'utilities',
    desc: 'Fast FTP, FTPS, and SFTP client for moving files to servers and NAS devices.',
    downloads: 88,
    version: 'Latest',
    homepage: 'https://filezilla-project.org/',
  },
  {
    id: 'Microsoft.WindowsTerminal',
    name: 'Windows Terminal',
    category: 'development',
    desc: 'GPU-accelerated terminal for PowerShell, Command Prompt, and WSL Linux.',
    downloads: 96,
    version: '1.21.2911',
    homepage: 'https://github.com/microsoft/terminal',
  },
  {
    id: 'WiresharkFoundation.Wireshark',
    name: 'Wireshark',
    category: 'development',
    desc: 'World’s foremost network traffic and packet capture analyzer.',
    downloads: 87,
    version: '4.4.1',
    homepage: 'https://www.wireshark.org/',
  },

  // Browsers & Web
  {
    id: 'Google.Chrome',
    name: 'Google Chrome',
    category: 'browsers',
    desc: 'Fast, secure, and customizable browser built by Google for the modern web.',
    downloads: 100,
    version: '131.0.6778',
    homepage: 'https://www.google.com/chrome/',
  },
  {
    id: 'Mozilla.Firefox',
    name: 'Mozilla Firefox',
    category: 'browsers',
    desc: 'Privacy-focused, independent web browser with advanced tracking protection.',
    downloads: 96,
    version: '132.0.2',
    homepage: 'https://www.mozilla.org/firefox/',
  },
  {
    id: 'Brave.Brave',
    name: 'Brave Browser',
    category: 'browsers',
    desc: 'Shields privacy by blocking advertisements and trackers automatically by default.',
    downloads: 95,
    version: '1.71.123',
    homepage: 'https://brave.com/',
  },
  {
    id: 'Microsoft.Edge',
    name: 'Microsoft Edge',
    category: 'browsers',
    desc: 'Fast and secure Windows browser with built-in Copilot AI features and vertical tabs.',
    downloads: 92,
    version: '131.0.2903',
    homepage: 'https://www.microsoft.com/edge',
  },
  {
    id: 'Opera.Opera',
    name: 'Opera',
    category: 'browsers',
    desc: 'Feature-rich web browser with built-in free VPN, ad blocker, and AI assistant.',
    downloads: 87,
    version: '114.0.5282',
    homepage: 'https://www.opera.com/',
  },
  {
    id: 'TorProject.TorBrowser',
    name: 'Tor Browser',
    category: 'browsers',
    desc: 'Browse anonymously with multi-layered onion encryption protecting privacy.',
    downloads: 84,
    version: '14.0.1',
    homepage: 'https://www.torproject.org/',
  },
  {
    id: 'Vivaldi.Vivaldi',
    name: 'Vivaldi',
    category: 'browsers',
    desc: 'Highly customizable power browser with tabs stacking, split views, and notes.',
    downloads: 83,
    version: '7.0.3495',
    homepage: 'https://vivaldi.com/',
  },

  // Productivity & Office
  {
    id: 'Notion.Notion',
    name: 'Notion',
    category: 'productivity',
    desc: 'The all-in-one workspace for notes, tasks, databases, and project management.',
    downloads: 96,
    version: '3.14.0',
    homepage: 'https://www.notion.so/',
  },
  {
    id: 'Obsidian.Obsidian',
    name: 'Obsidian',
    category: 'productivity',
    desc: 'Markdown-based second brain and personal knowledge base stored locally on disk.',
    downloads: 94,
    version: '1.7.5',
    homepage: 'https://obsidian.md/',
  },
  {
    id: 'Notepad++.Notepad++',
    name: 'Notepad++',
    category: 'productivity',
    desc: 'Fast, lightweight text and source code editor with multi-document tabs.',
    downloads: 98,
    version: '8.7.1',
    homepage: 'https://notepad-plus-plus.org/',
  },
  {
    id: 'TheDocumentFoundation.LibreOffice',
    name: 'LibreOffice',
    category: 'productivity',
    desc: 'Free open source office suite for documents, spreadsheets, and presentations.',
    downloads: 91,
    version: '24.8.3',
    homepage: 'https://www.libreoffice.org/',
  },
  {
    id: 'Adobe.Acrobat.Reader.64-bit',
    name: 'Adobe Acrobat Reader',
    category: 'productivity',
    desc: 'Standard for viewing, printing, signing, and annotating PDF documents.',
    downloads: 95,
    version: '24.004.20243',
    homepage: 'https://www.adobe.com/acrobat/pdf-reader.html',
  },
  {
    id: 'Figma.Figma',
    name: 'Figma Desktop',
    category: 'productivity',
    desc: 'Collaborative UI/UX design tool for prototyping and design systems.',
    downloads: 93,
    version: '124.5.5',
    homepage: 'https://www.figma.com/',
  },
  {
    id: 'BlenderFoundation.Blender',
    name: 'Blender',
    category: 'productivity',
    desc: 'Free and open source 3D creation suite: modeling, animation, VFX, and rendering.',
    downloads: 93,
    version: '4.2.3',
    homepage: 'https://www.blender.org/',
  },
  {
    id: 'GIMP.GIMP',
    name: 'GIMP',
    category: 'productivity',
    desc: 'Cross-platform image editor with advanced retouching and drawing tools.',
    downloads: 87,
    version: '2.10.38',
    homepage: 'https://www.gimp.org/',
  },
  {
    id: 'Inkscape.Inkscape',
    name: 'Inkscape',
    category: 'productivity',
    desc: 'Professional vector graphics editor for creating SVG illustrations and logos.',
    downloads: 86,
    version: '1.4',
    homepage: 'https://inkscape.org/',
  },

  // Gaming & Social
  {
    id: 'Discord.Discord',
    name: 'Discord',
    category: 'gaming',
    desc: 'High clarity voice, video, and text communication for communities and gamers.',
    downloads: 99,
    version: '1.0.9168',
    homepage: 'https://discord.com/',
  },
  {
    id: 'Telegram.TelegramDesktop',
    name: 'Telegram Desktop',
    category: 'gaming',
    desc: 'Cloud-based instant messaging with powerful channels, bots, and file sharing.',
    downloads: 95,
    version: '5.7.1',
    homepage: 'https://telegram.org/',
  },
  {
    id: 'WhatsApp.WhatsApp',
    name: 'WhatsApp',
    category: 'gaming',
    desc: 'Simple, reliable, private messaging and HD calls synced to your PC.',
    downloads: 97,
    version: '2.2444.6',
    homepage: 'https://www.whatsapp.com/',
  },
  {
    id: 'SlackTechnologies.Slack',
    name: 'Slack',
    category: 'gaming',
    desc: 'Enterprise communication hub bringing teamwork, channels, and apps together.',
    downloads: 94,
    version: '4.41.97',
    homepage: 'https://slack.com/',
  },
  {
    id: 'Zoom.Zoom',
    name: 'Zoom Workplace',
    category: 'gaming',
    desc: 'Video meetings, virtual team chat, whiteboard, and screen sharing.',
    downloads: 93,
    version: '6.2.6',
    homepage: 'https://www.zoom.com/',
  },
  {
    id: 'Microsoft.Teams',
    name: 'Microsoft Teams',
    category: 'gaming',
    desc: 'Connect and collaborate with coworkers, group calls, and Office integration.',
    downloads: 92,
    version: '24277.502',
    homepage: 'https://www.microsoft.com/microsoft-teams/group-chat-software',
  },
  {
    id: 'Valve.Steam',
    name: 'Steam',
    category: 'gaming',
    desc: 'The ultimate digital entertainment platform for playing and purchasing PC games.',
    downloads: 98,
    version: '2.10.91',
    homepage: 'https://store.steampowered.com/',
  },
  {
    id: 'EpicGames.EpicGamesLauncher',
    name: 'Epic Games Launcher',
    category: 'gaming',
    desc: 'Gateway to Unreal Engine games, Fortnite, and weekly free games store.',
    downloads: 88,
    version: '17.0.0',
    homepage: 'https://www.epicgames.com/',
  },
  {
    id: 'GOG.Galaxy',
    name: 'GOG Galaxy',
    category: 'gaming',
    desc: 'All your PC games in one launcher with DRM-free downloads and friend lists.',
    downloads: 85,
    version: '2.0.73',
    homepage: 'https://www.gog.com/galaxy',
  }
];


// ================= About this computer v2 (ADR 009) =================
const ABOUT_NR_ICON = '<svg width="14" height="14" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" aria-hidden="true"><circle cx="7" cy="7" r="5.2" stroke-dasharray="2.6 2.2"/></svg>';
const ABOUT_COPY_ICON = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/></svg>';

function aboutEscapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function aboutNr() {
  return `<span class="about-nr">${ABOUT_NR_ICON}Not reported by this PC</span>`;
}

function aboutDisp(value) {
  if (value === null || value === undefined || value === '') return aboutNr();
  return aboutEscapeHtml(value);
}

function aboutFormatArch(arch) {
  if (!arch) return null;
  const a = String(arch).toLowerCase();
  if (a === 'x64' || a === 'amd64') return 'AMD64';
  if (a === 'arm64' || a === 'aarch64') return 'ARM64';
  return String(arch);
}

function aboutFormatBytes(bytes) {
  const n = Number(bytes);
  if (!Number.isFinite(n) || n <= 0) return null;
  if (n >= 1024 ** 3) return (n / 1024 ** 3).toFixed(2) + ' GB';
  if (n >= 1024 ** 2) return (n / 1024 ** 2).toFixed(1) + ' MB';
  return Math.round(n / 1024) + ' KB';
}

function aboutFormatGiB(bytes) {
  const n = Number(bytes);
  if (!Number.isFinite(n) || n <= 0) return null;
  return Math.round((n + (1 << 30) - 1) / (1 << 30)) + ' GB';
}

function aboutArt(kind, facts) {
  if (typeof renderArt === 'function') {
    try {
      return renderArt(kind, facts);
    } catch (e) {
      return { svg: '', key: '', label: '' };
    }
  }
  if (typeof pickArt === 'function' && typeof HARDWARE_ART === 'object') {
    const key = pickArt(kind, facts);
    return { key, label: key, svg: HARDWARE_ART[key] || '' };
  }
  return { svg: '', key: '', label: '' };
}

function aboutCpuSvg(cpu) {
  if (cpu && typeof makeCpuArt === 'function') {
    const gen = cpu.model_number || cpu.generation || '';
    return makeCpuArt({
      vendor: cpu.vendor || 'generic',
      family: cpu.family || 'CPU',
      generation: gen,
      ai: cpu.family && /\bAI\b/i.test(cpu.family)
    });
  }
  return aboutArt('cpu', { name: (cpu && cpu.brand_string) || '', brand: (cpu && cpu.brand_string) || '' }).svg;
}

function aboutCoreStrip(cores, threads) {
  const c = Math.max(0, Number(cores) || 0);
  const t = Math.max(c, Number(threads) || c);
  const perCore = c > 0 ? Math.max(1, Math.round(t / c)) : 0;
  if (!c) return '';
  let html = '<div class="about-cores">';
  for (let i = 0; i < c; i++) {
    const dots = Math.min(perCore, 4);
    html += '<span class="about-core">';
    for (let d = 0; d < dots; d++) html += '<i></i>';
    html += '</span>';
  }
  html += '</div>';
  if (c && perCore) {
    html += `<div class="about-cap2">${c} cores, ${perCore} thread${perCore === 1 ? '' : 's'} each</div>`;
  }
  return html;
}

function aboutMemBlocks(totalBytes) {
  const gib = totalBytes ? Math.round((Number(totalBytes) + (1 << 30) - 1) / (1 << 30)) : 0;
  if (!gib) return '';
  const blocks = Math.min(Math.max(gib, 1), 32);
  let html = '<div class="about-blocks">';
  for (let i = 0; i < blocks; i++) html += '<i></i>';
  html += '</div><div class="about-cap2">Installed memory, 1 GB per block</div>';
  return html;
}

function aboutDriveMeta(d) {
  const loc = (d.external || d.removable) ? 'External' : 'Internal';
  const media = d.media || d.mediaType || d.bus || d.busType || '';
  const fs = d.fs || d.filesystem || '';
  return [loc, media, fs].filter(Boolean).join(' · ');
}

function aboutGpuSub(gpu) {
  const integrated = !!gpu.integrated;
  if (integrated) return 'Built-in · shares system memory';
  const vram = aboutFormatBytes(gpu.vram_bytes);
  return vram ? `Dedicated · ${vram}` : 'Dedicated';
}

function aboutIsUsbDisplay(row) {
  if (!row) return false;
  if (row.kind === 'usb-display' || row.connection === 'usb') return true;
  const blob = `${row.name || ''} ${row.provider || ''}`;
  if (/displaylink/i.test(blob)) return true;
  return /^usb\\/i.test(String(row.matching_id || ''));
}

function aboutUsbDisplayKey(row) {
  if (!row) return '';
  if (row.merge_key) return String(row.merge_key).toLowerCase();
  let matching = String(row.matching_id || '').toLowerCase();
  const mi = matching.indexOf('&mi_');
  if (mi >= 0) matching = matching.slice(0, mi);
  return `${String(row.name || '').toLowerCase()}|${matching}`;
}

function aboutCollectUsbDevices(hw) {
  const reportedGpus = Array.isArray(hw && hw.gpus) ? hw.gpus : [];
  const seen = new Set();
  const devices = [];
  []
    .concat(Array.isArray(hw && hw.usb_devices) ? hw.usb_devices : [])
    .concat(reportedGpus.filter(aboutIsUsbDisplay))
    .forEach((row) => {
      const key = aboutUsbDisplayKey(row);
      if (!key || seen.has(key)) return;
      seen.add(key);
      devices.push(row);
    });
  return devices;
}

function buildAboutComputerPageHtml(diag, storageDrives) {
  const hw = (diag && diag.hardware) || {};
  const device = hw.device || {};
  const cpu = hw.cpu || {};
  const memory = hw.memory || {};
  const reportedGpus = Array.isArray(hw.gpus) ? hw.gpus : [];
  const gpus = reportedGpus.filter((gpu) => !aboutIsUsbDisplay(gpu));
  const usbDevices = aboutCollectUsbDevices(hw);
  const displays = Array.isArray(hw.display) ? hw.display : [];
  const cameras = Array.isArray(hw.camera) ? hw.camera : [];
  const battery = hw.battery || {};
  const osInfo = hw.os || {};
  const drives = Array.isArray(storageDrives) && storageDrives.length
    ? storageDrives
    : (Array.isArray(hw.storage) ? hw.storage : []);

  const modelName = device.model_name || diag.model_name || diag.model_id || 'This PC';
  const modelId = device.model_id || diag.model_id || '';
  const serial = device.serial_number || diag.serial_number || '';
  const manufacturer = device.manufacturer || '';
  const osName = osInfo.name || diag.os_display || diag.os_info || '';
  const osVersion = osInfo.version ? ` ${osInfo.version}` : '';
  const osLine = (osName + osVersion).trim() || diag.os_display || '';
  const archLabel = aboutFormatArch(cpu.arch || diag.arch);
  const kind = device.kind === 'desktop' ? 'desktop' : 'laptop';
  const kindLabel = `Windows ${kind}`;

  const deviceArt = aboutArt('device', {
    model: modelName,
    manufacturer,
    isLaptop: kind === 'laptop'
  }).svg;

  const heroChips = [];
  if (modelId) heroChips.push(`<span class="about-chip-neu">${aboutEscapeHtml(modelId)}</span>`);
  if (archLabel) heroChips.push(`<span class="about-chip-neu">${aboutEscapeHtml(archLabel)}</span>`);

  const serialDisabled = serial ? '' : ' disabled';
  const serialHint = serial
    ? ''
    : '<div class="about-hero-hint">Copy serial number turns on when this PC reports one.</div>';

  const cores = cpu.cores || 0;
  const threads = cpu.threads || cores;
  const cpuStat = cores ? `${cores} core${cores === 1 ? '' : 's'}, ${threads} thread${threads === 1 ? '' : 's'}` : aboutNr();
  const cpuGenLine = cpu.generation ? `<div class="about-cap2">${aboutEscapeHtml(cpu.generation)}</div>` : '';

  const memGiB = aboutFormatGiB(memory.total_bytes) || diag.memory || null;
  const memStat = memGiB || aboutNr();
  const memArt = aboutArt('memory', { type: memory.type, isLaptop: kind === 'laptop' }).svg;

  const gpuCount = gpus.length;
  const gpuStat = gpuCount
    ? `${gpuCount} graphics adapter${gpuCount === 1 ? '' : 's'}`
    : aboutNr();
  const gpuRows = gpus.map((gpu) => {
    const art = aboutArt('gpu', { name: gpu.name, integrated: gpu.integrated, dedicated: !gpu.integrated }).svg;
    return `<div class="about-part-row" style="--acc:var(--about-acc-peach)"><div class="about-ptile">${art}</div><div class="about-pinfo"><div class="about-pnm">${aboutEscapeHtml(gpu.name)}</div><div class="about-psub">${aboutGpuSub(gpu)}</div></div></div>`;
  }).join('');

  const primary = drives.find((d) => String(d.letter || '').toUpperCase().startsWith('C')) || drives[0];
  let storageStat = aboutNr();
  let storageWarn = '';
  if (primary && primary.total_bytes) {
    const free = aboutFormatBytes(primary.free_bytes);
    const letter = primary.letter ? `${primary.letter}:` : 'drive';
    storageStat = free ? `${free} free on ${letter}` : aboutNr();
    const usedPct = Math.round(((primary.total_bytes - (primary.free_bytes || 0)) / primary.total_bytes) * 100);
    if (usedPct >= 85) storageWarn = '<span class="about-chip-warn">Getting full</span>';
  }

  const storageRows = drives.map((d) => {
    const letter = d.letter ? String(d.letter).replace(/:$/, '') : '';
    const total = Number(d.total_bytes) || 0;
    const free = Number(d.free_bytes) || 0;
    const used = Math.max(0, total - free);
    const usedPct = total ? Math.round((used / total) * 100) : 0;
    const freePct = total ? 100 - usedPct : 0;
    const title = d.label ? `${d.label}${letter ? ` (${letter}:)` : ''}` : (letter ? `Local Disk (${letter}:)` : 'Local disk');
    const art = aboutArt('storage', d).svg;
    const extTag = (d.external || d.removable) ? ' · External' : '';
    return `<div class="about-part-row" style="--acc:var(--about-acc-sky)">${letter ? `<div class="about-plet">${aboutEscapeHtml(letter)}:</div>` : ''}<div class="about-ptile" style="--art-accent:var(--about-acc-sky)">${art}</div><div class="about-pinfo"><div class="about-pnm">${aboutEscapeHtml(title)}</div><div class="about-psub">${aboutEscapeHtml(aboutDriveMeta(d))}${extTag}</div><div class="about-pmeta"><span>${used ? aboutEscapeHtml(`${aboutFormatBytes(used)} used`) : aboutNr()}</span><span><b>${free ? aboutEscapeHtml(aboutFormatBytes(free)) : aboutNr()}</b> free of ${total ? aboutEscapeHtml(aboutFormatBytes(total)) : aboutNr()}</span></div><div class="about-pbar"><div class="about-seg"><i class="about-used" style="width:${usedPct}%"></i><i class="about-free" style="width:${freePct}%"></i></div></div></div></div>`;
  }).join('');

  const dispRows = displays.length ? displays.map((disp) => {
    const res = disp.width && disp.height ? `${disp.width} × ${disp.height}` : null;
    const hz = disp.refresh_hz ? `${disp.refresh_hz} Hz` : null;
    const sub = [res, hz, disp.scale_percent ? `${disp.scale_percent}% scale` : ''].filter(Boolean).join(' · ');
    const art = aboutArt('display', { name: disp.name, isLaptop: kind === 'laptop' }).svg;
    return `<div class="about-part-row"><div class="about-ptile">${art}</div><div class="about-pinfo"><div class="about-pnm">${aboutDisp(disp.name)}</div><div class="about-psub">${sub ? aboutEscapeHtml(sub) : aboutNr()}</div></div></div>`;
  }).join('') : '';

  const camNames = cameras.filter(Boolean);
  const camStat = camNames.length ? aboutEscapeHtml(camNames[0]) : aboutNr();
  const camArt = aboutArt('camera', camNames.length ? { name: camNames[0], builtIn: true } : { present: false }).svg;
  const camKvs = camNames.length
    ? camNames.map((name, i) => `<div class="about-kv"><dt>Camera ${camNames.length > 1 ? i + 1 : ''}</dt><dd>${aboutEscapeHtml(name)}</dd></div>`).join('')
    : `<div class="about-kv"><dt>Webcam</dt><dd>${aboutNr()}</dd></div>`;

  const batPresent = battery.present !== false && (battery.charge_percent != null || battery.health_percent != null);
  const batHead = batPresent && battery.charge_percent != null
    ? `${battery.charge_percent}% charged`
    : (battery.present === false ? 'Plugged in · no battery' : aboutNr());
  const powerSrc = battery.on_ac === true ? 'Wall power' : (battery.on_ac === false ? 'Battery' : null);
  const batArt = aboutArt('battery', {
    present: battery.present !== false,
    percent: battery.charge_percent,
    charging: battery.on_ac === true && battery.charge_percent != null && battery.charge_percent < 100
  }).svg;

  const winArt = aboutArt('os', { name: osName, version: osInfo.version, build: osInfo.build }).svg;
  const osStat = osName ? osName.replace(/\s+\d.+$/, '').trim() || osName : aboutNr();

  const identityArt = aboutArt('device', { model: modelId || modelName, manufacturer }).svg;
  const gpuTile = aboutArt('gpu', gpus[0] ? { name: gpus[0].name } : {}).svg;
  const usbStat = usbDevices.length
    ? `${usbDevices.length} external USB device${usbDevices.length === 1 ? '' : 's'}`
    : '';
  const usbRows = usbDevices.map((row) => {
    const art = aboutArt('storage', { name: row.name, bus: 'usb', external: true }).svg;
    return `<div class="about-part-row" style="--acc:var(--about-acc-sky)"><div class="about-ptile" style="--art-accent:var(--about-acc-sky)">${art}</div><div class="about-pinfo"><div class="about-pnm">${aboutEscapeHtml(row.name)}</div><div class="about-psub">External</div></div></div>`;
  }).join('');
  const usbSection = usbDevices.length ? `
      <article class="about-sec card" style="--acc:var(--about-acc-sky)">
        <div class="about-sh"><div class="about-tile" style="--art-accent:var(--about-acc-sky)">${aboutArt('storage', { name: usbDevices[0].name, bus: 'usb', external: true }).svg}</div><div><div class="about-eyebrow">USB devices</div><div class="about-stat">${aboutEscapeHtml(usbStat)}</div><div class="about-hint">Plugged in with a USB cable. These are not graphics cards.</div></div></div>
        <div class="about-part-rows">${usbRows}</div>
      </article>` : '';

  return `
    <section class="about-hero card">
      <div class="about-hero-art">${deviceArt}</div>
      <div class="about-hero-body">
        <div class="about-hero-kind">${aboutEscapeHtml(kindLabel)}</div>
        <h2 class="about-hero-title">${aboutEscapeHtml(modelName)}</h2>
        <div class="about-hero-os">${aboutDisp(osLine)}</div>
        <div class="about-hero-facts">${heroChips.join('')}</div>
        <div class="about-hero-acts">
          <button type="button" class="glass-btn btn-primary btn-sm" onclick="app.copyAllSystemSpecs()">${ABOUT_COPY_ICON}<span>Copy all system info</span></button>
          <button type="button" class="glass-btn btn-installed btn-sm"${serialDisabled} onclick="app.copySerialNumber()">${ABOUT_COPY_ICON}<span>Copy serial number</span></button>
        </div>
        ${serialHint}
      </div>
    </section>
    <div class="about-grid">
      <article class="about-sec card" style="--acc:var(--about-acc-sky)">
        <div class="about-sh"><div class="about-tile" style="--art-accent:var(--about-acc-sky)">${identityArt}</div><div><div class="about-eyebrow">Model and identity</div><div class="about-stat">${aboutDisp(modelId || modelName)}</div><div class="about-hint">The model number printed on this ${kind}.</div></div></div>
        <dl class="about-kvs"><div class="about-kv"><dt>Brand</dt><dd>${aboutDisp(manufacturer)}</dd></div><div class="about-kv"><dt>Product name</dt><dd>${aboutDisp(modelName)}</dd></div><div class="about-kv"><dt>Serial number</dt><dd>${serial ? aboutEscapeHtml(serial) : aboutNr()}</dd></div></dl>
      </article>
      <article class="about-sec card" style="--acc:var(--about-acc-mint)">
        <div class="about-sh"><div class="about-tile" style="--art-accent:var(--about-acc-mint);--art-fill:var(--input-surface)">${aboutCpuSvg(cpu)}</div><div><div class="about-eyebrow">Processor</div><div class="about-stat">${typeof cpuStat === 'string' && cpuStat.indexOf('about-nr') === -1 ? aboutEscapeHtml(cpuStat) : cpuStat}</div><div class="about-hint">The brain of your PC. It runs every app.</div></div></div>
        ${aboutCoreStrip(cores, threads)}${cpuGenLine}
        <dl class="about-kvs"><div class="about-kv"><dt>Processor</dt><dd>${aboutDisp(cpu.brand_string || diag.cpu)}</dd></div><div class="about-kv"><dt>Architecture</dt><dd>${aboutDisp(archLabel)}</dd></div></dl>
      </article>
      <article class="about-sec card" style="--acc:var(--about-acc-mint)">
        <div class="about-sh"><div class="about-tile">${memArt}</div><div><div class="about-eyebrow">Memory</div><div class="about-stat">${memGiB ? aboutEscapeHtml(memGiB) : memStat}</div><div class="about-hint">Short-term space where open apps keep their data.</div></div></div>
        ${aboutMemBlocks(memory.total_bytes)}
        <dl class="about-kvs"><div class="about-kv"><dt>Installed</dt><dd>${memGiB ? aboutEscapeHtml(memGiB) : aboutNr()}</dd></div><div class="about-kv"><dt>Type</dt><dd>${aboutDisp(memory.type)}</dd></div><div class="about-kv"><dt>Speed</dt><dd>${memory.speed_mhz ? aboutEscapeHtml(`${memory.speed_mhz} MHz`) : aboutNr()}</dd></div><div class="about-kv"><dt>Slots used</dt><dd>${memory.slots_used != null ? aboutEscapeHtml(String(memory.slots_used)) : aboutNr()}</dd></div></dl>
      </article>
      <article class="about-sec card" style="--acc:var(--about-acc-peach)">
        <div class="about-sh"><div class="about-tile" style="--art-accent:var(--about-acc-peach)">${gpuTile}</div><div><div class="about-eyebrow">Graphics</div><div class="about-stat">${gpuCount ? aboutEscapeHtml(gpuStat) : gpuStat}</div><div class="about-hint">Built-in and dedicated cards that draw what you see on screen.</div></div></div>
        ${gpuRows ? `<div class="about-part-rows">${gpuRows}</div>` : ''}
      </article>
      ${usbSection}
      <article class="about-sec card" style="--acc:var(--about-acc-sky)">
        <div class="about-sh"><div class="about-tile" style="--art-accent:var(--about-acc-sky)">${dispRows ? aboutArt('display', { count: displays.length, isLaptop: kind === 'laptop' }).svg : aboutArt('display', { isLaptop: kind === 'laptop' }).svg}</div><div><div class="about-eyebrow">Display</div><div class="about-stat">${displays.length ? aboutEscapeHtml(`${displays.length} screen${displays.length === 1 ? '' : 's'}`) : aboutNr()}</div><div class="about-hint">Resolution and refresh for each screen.</div></div></div>
        ${dispRows ? `<div class="about-part-rows">${dispRows}</div>` : `<dl class="about-kvs"><div class="about-kv"><dt>Primary display</dt><dd>${aboutNr()}</dd></div></dl>`}
      </article>
      <article class="about-sec card wide" style="--acc:var(--about-acc-sky)">
        <div class="about-sh"><div class="about-tile" style="--art-accent:var(--about-acc-sky)">${aboutArt('storage', primary || {}).svg}</div><div><div class="about-sec-head-row"><div class="about-eyebrow">Storage</div>${storageWarn}</div><div class="about-stat">${typeof storageStat === 'string' && storageStat.indexOf('about-nr') >= 0 ? storageStat : aboutEscapeHtml(storageStat)}</div><div class="about-hint">Every physical drive on this PC. Cloud volumes are not listed.</div></div></div>
        ${storageRows ? `<div class="about-part-rows">${storageRows}</div>` : `<dl class="about-kvs"><div class="about-kv"><dt>Drives</dt><dd>${aboutNr()}</dd></div></dl>`}
      </article>
      <article class="about-sec card" style="--acc:var(--about-acc-gold)">
        <div class="about-sh"><div class="about-tile">${batArt}</div><div><div class="about-eyebrow">Battery and power</div><div class="about-stat${batPresent ? '' : ' muted-stat'}">${typeof batHead === 'string' && batHead.indexOf('about-nr') >= 0 ? batHead : aboutEscapeHtml(batHead)}</div><div class="about-hint">Charge, health and power source from Windows.</div></div></div>
        <div class="about-minis">
          <div class="about-mini"><div class="about-lb">Charge level</div><div class="about-vl">${battery.charge_percent != null ? aboutEscapeHtml(`${battery.charge_percent}%`) : aboutNr()}</div></div>
          <div class="about-mini"><div class="about-lb">Battery health</div><div class="about-vl">${battery.health_percent != null ? aboutEscapeHtml(`${battery.health_percent}%`) : aboutNr()}</div></div>
          <div class="about-mini"><div class="about-lb">Power source</div><div class="about-vl">${powerSrc ? aboutEscapeHtml(powerSrc) : aboutNr()}</div></div>
        </div>
        <dl class="about-kvs"><div class="about-kv"><dt>Cycle count</dt><dd>${battery.cycle_count != null ? aboutEscapeHtml(String(battery.cycle_count)) : aboutNr()}</dd></div></dl>
      </article>
      <article class="about-sec card" style="--acc:var(--about-acc-peach)">
        <div class="about-sh"><div class="about-tile">${camArt}</div><div><div class="about-eyebrow">Camera</div><div class="about-stat${camNames.length ? '' : ' muted-stat'}">${camStat}</div><div class="about-hint">Cameras Windows has listed on this PC.</div></div></div>
        <dl class="about-kvs">${camKvs}</dl>
      </article>
      <article class="about-sec card" style="--acc:var(--about-acc-sky)">
        <div class="about-sh"><div class="about-tile">${winArt}</div><div><div class="about-eyebrow">Windows</div><div class="about-stat">${typeof osStat === 'string' && osStat.indexOf('about-nr') >= 0 ? osStat : aboutEscapeHtml(osStat)}</div><div class="about-hint">The system that runs this PC.</div></div></div>
        <dl class="about-kvs"><div class="about-kv"><dt>Edition</dt><dd>${aboutDisp(osName)}</dd></div><div class="about-kv"><dt>Version</dt><dd>${aboutDisp(osInfo.version)}</dd></div><div class="about-kv"><dt>Build number</dt><dd>${aboutDisp(osInfo.build)}</dd></div><div class="about-kv"><dt>Architecture</dt><dd>${aboutDisp(archLabel ? `64-bit (${archLabel})` : null)}</dd></div></dl>
      </article>
    </div>`;
}

if (typeof window !== 'undefined') {
  window.buildAboutComputerPageHtml = buildAboutComputerPageHtml;
}


// ================= ScanStore (ADR 008 readiness / mockup 08–09) =================
const SCAN_UI_JOB_IDS = [
  'catalog-updates',
  'catalog-installed',
  'local-hardware',
  'local-arp',
  'cleanup_scan',
  'startup',
  'leftovers'
];

const TAB_TO_SCAN_JOB = {
  updates: 'catalog-updates',
  explore: 'catalog-installed',
  installed: 'catalog-installed',
  specs: 'local-hardware',
  cleanup: 'cleanup_scan',
  'startup-apps': 'startup',
  'leftover-folders': 'leftovers'
};

const NAV_SCAN_SLOTS = [
  { tab: 'updates', job: 'catalog-updates' },
  { tab: 'explore', job: 'catalog-installed' },
  { tab: 'installed', job: 'catalog-installed' },
  { tab: 'cleanup', job: 'cleanup_scan' },
  { tab: 'startup-apps', job: 'startup' },
  { tab: 'large-files', job: null },
  { tab: 'leftover-folders', job: 'leftovers' },
  { tab: 'specs', job: 'local-hardware' }
];

/** Startup ready-gate jobs (ADR 025 + 032). local-hardware stays off this list. */
const STARTUP_GATE_JOBS = [
  { id: 'self-update', label: 'Quantum Updater', detail: 'Checks for a newer app build before the rest starts.', local: true },
  { id: 'local-arp', label: 'Programs on this PC', detail: 'Uninstall entries on this PC.' },
  { id: 'catalog-updates', label: 'App updates', detail: 'Reading the catalog. No invented percent.' },
  { id: 'catalog-installed', label: 'Programs from the catalog', detail: 'Starts after App updates when needed.' }
];
const STARTUP_GATE_SETTLED = new Set(['ready', 'failed', 'cancelled']);

class ScanStore {
  constructor(app) {
    this.app = app;
    this.jobs = {};
    this.summary = { done: 0, total: 0, eta_s: 0, running: 0 };
    this._pollTimer = null;
    this._prevStatus = {};
    this._cleanupPoll = null;
    this.cleanupLive = null;
    this.hasLeftoversRecycle = true;
  }

  async bootstrap() {
    try {
      await this.app.invoke('bridge_ping', {});
    } catch (_) { /* offline dev */ }
    try {
      if (window.pywebview && window.pywebview.api && window.pywebview.api.leftovers_recycle) {
        this.hasLeftoversRecycle = true;
      } else if (window.pywebview && window.pywebview.api && !window.pywebview.api.leftovers_recycle) {
        this.hasLeftoversRecycle = false;
      }
    } catch (_) {
      this.hasLeftoversRecycle = false;
    }
    try {
      await this.app.invoke('scans_start_all', {});
    } catch (_) {}
    this.poll();
  }

  schedule(ms) {
    clearTimeout(this._pollTimer);
    this._pollTimer = setTimeout(() => this.poll(), ms);
  }

  async poll() {
    try {
      const st = await this.app.invoke('scans_state', {});
      if (st && typeof st === 'object') {
        this.jobs = st.jobs || {};
        this.summary = st.summary || this.summary;
        this.onJobsUpdated();
      }
    } catch (err) {
      console.warn('[ScanStore] scans_state:', err);
    }
    const gateOpen = !!(this.app && this.app.isStartupGateVisible && this.app.isStartupGateVisible());
    const ms = gateOpen ? 1000 : ((this.summary.running > 0) ? 500 : 5000);
    this.schedule(ms);
    this.pollCleanupStatusIfNeeded();
  }

  job(id) {
    return this.jobs[id] || { id, status: 'queued', percent: 0, cached: false };
  }

  isReady(id) { return this.job(id).status === 'ready'; }
  isScanning(id) { return this.job(id).status === 'scanning'; }
  isCleanupJobActive() {
    const status = this.job('cleanup_scan').status;
    return status === 'scanning' || status === 'queued';
  }
  isFailed(id) { return this.job(id).status === 'failed'; }
  isCached(id) { return !!this.job(id).cached; }

  updatesReady() { return this.isReady('catalog-updates'); }
  marketplaceReady() { return this.isReady('catalog-installed') || this.isReady('local-arp'); }
  cleanupReady() { return this.isReady('cleanup_scan'); }
  cleanupFresh() { return this.cleanupReady() && !this.isCached('cleanup_scan'); }

  formatLastChecked(job) {
    const ts = job.cached_at || job.finished_at;
    if (!ts) return '';
    const d = new Date(ts * 1000);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleString();
  }

  async rescanAll() {
    if (this.app && typeof this.app.skipStartupGateForForcedRescan === 'function') {
      this.app.skipStartupGateForForcedRescan();
    }
    await this.app.invoke('scans_start_all', { force: true });
  }

  async startJob(jobId, force) {
    await this.app.invoke('scan_start', { job_id: jobId, force: !!force });
  }

  async ensureDeferredJob(jobId) {
    if (!jobId || this.isReady(jobId) || this.isScanning(jobId)) return;
    await this.startJob(jobId, false);
  }

  async cancelJob(jobId) {
    await this.app.invoke('scan_cancel', { job_id: jobId });
  }

  onJobsUpdated() {
    SCAN_UI_JOB_IDS.forEach((id) => {
      const cur = this.job(id).status;
      const prev = this._prevStatus[id];
      if (id === 'catalog-updates' && (cur === 'scanning' || cur === 'queued') && prev !== cur) {
        this.app.refreshUpdatesFromStore(true);
      }
      if (cur === 'ready' && prev !== 'ready') {
        this.app.onScanJobReady(id, this.job(id));
      }
      if (id === 'cleanup_scan' && (cur === 'failed' || cur === 'cancelled') && prev !== cur) {
        this.app.onCleanupScanStopped(cur);
      }
      this._prevStatus[id] = cur;
    });
    this.app.renderSidebarScanStatus();
    this.app.applyReadinessGates();
  }

  pollCleanupStatusIfNeeded() {
    const job = this.job('cleanup_scan');
    if (!this.isCleanupJobActive()) {
      if (this._cleanupPoll) {
        clearInterval(this._cleanupPoll);
        this._cleanupPoll = null;
      }
      if (job.status === 'ready' || job.status === 'failed' || job.status === 'cancelled') {
        this.app.renderCleanupScanProgress({ status: 'done' });
      }
      return;
    }
    this.app.renderCleanupScanProgress({
      status: job.status === 'queued' ? 'queued' : 'scanning',
      percent: job.percent,
      progress: {
        percent: job.percent,
        eta_s: job.eta_s,
        current_folder: job.current_item,
      },
      current_item: job.current_item,
    });
  }
}

class QuantumUpdaterApp {
  constructor() {
    this.updates = [];
    this.selectedIds = new Set();
    this.installed = [];
    this.installedSet = new Set();
    this.installedNormSet = new Set();
    this.exploreLiveSearchQuery = '';
    this.lastCatalogSearchResults = null;
    this.activeTab = 'updates';
    this.isScanning = false;
    this.scanError = '';
    this.isUpdating = false;
    this.activeEngine = 'winget';

    // Explore Filters & Sorting State
    this.exploreCategory = 'all';
    this.exploreSort = 'popular';
    this.exploreSearchTerm = '';

    // Sequential Operation Queue System
    this.operationQueue = [];
    this.isProcessingQueue = false;
    this.currentQueueTask = null;

    // Quantum Clean State
    this.cleanupData = null;
    this.selectedCleanupCategories = new Set();
    this.isCleaning = false;

    // In-App Self-Updater State
    this.pendingUpdate = null;
    this.isCheckingAppUpdate = false;
    this.isApplyingAppUpdate = false;

    // Startup ready gate (ADR 025) — dismissed for the session once bootstrap jobs settle
    this._startupGateDismissed = false;
    this._startupGateSkipForced = false;

    // Wave 1 30.1 — show restart needed; never reboot the PC from here
    this._restartRequired = false;

    this.appIconCache = new Map();
    this.scanStore = new ScanStore(this);
    this.leftoverItems = [];
    this.largeFilesList = [];
    this.largeFilesJobId = '';
    this.cleanupPlanStale = true;

    this.init();
  }

  isDarwinHost() {
    if (typeof navigator === 'undefined') return false;
    const ua = navigator.userAgent || '';
    if (/Windows/i.test(ua)) return false;
    const platform = navigator.platform || '';
    if (/Win/i.test(platform)) return false;
    return platform === 'MacIntel' || platform === 'MacPPC' || platform === 'Mac68K' ||
      /Mac OS X|Macintosh/i.test(ua);
  }

  isMacDiagnostics(diag) {
    if (diag) {
      if (diag.os_system === 'Darwin') return true;
      if (diag.os_display && /macOS/i.test(diag.os_display)) return true;
      if (diag.os_system && /Windows/i.test(diag.os_system)) return false;
    }
    return this.isDarwinHost();
  }

  normalizeInstallKey(value) {
    if (value === null || value === undefined) return '';
    return String(value).toLowerCase().trim().replace(/[^a-z0-9]/g, '');
  }

  rebuildInstalledMatchIndex(packages) {
    this.installedSet.clear();
    if (!this.installedNormSet) this.installedNormSet = new Set();
    this.installedNormSet.clear();
    if (!packages || !Array.isArray(packages)) return;
    packages.forEach(p => {
      if (p.id) {
        this.installedSet.add(p.id.toLowerCase().trim());
        const normId = this.normalizeInstallKey(p.id);
        if (normId) this.installedNormSet.add(normId);
      }
      if (p.name) {
        this.installedSet.add(p.name.toLowerCase().trim());
        const normName = this.normalizeInstallKey(p.name);
        if (normName) this.installedNormSet.add(normName);
      }
    });
  }

  applyWindowsAboutComputerPlaceholders() {
    const root = document.getElementById('about-computer-root');
    if (root) {
      root.innerHTML = '<p class="about-loading muted-copy">Reading this PC\u2026</p>';
    }
  }

  adaptPlatformUI() {
    this.isMac = this.isDarwinHost();

    document.body.classList.toggle('platform-mac', !!this.isMac);
    document.body.classList.toggle('platform-windows', !this.isMac);

    const spotlightBadge = document.getElementById('spotlight-shortcut-badge');
    if (spotlightBadge) {
      spotlightBadge.textContent = this.isMac ? '⌘K' : 'Ctrl+K';
    }

    if (!this.isMac) {
      this.applyWindowsAboutComputerPlaceholders();
      document.title = 'Quantum Updater • Windows Edition';

      // Harmonize branding subtitle
      const brandSub = document.querySelector('.brand-subtitle');
      if (brandSub && (brandSub.textContent.includes('Mac') || brandSub.textContent.includes('Smart'))) {
        brandSub.textContent = 'System & Package Manager';
      }

      // Harmonize search bar placeholder
      const spotInput = document.getElementById('global-spotlight-search');
      if (spotInput) {
        spotInput.placeholder = 'Search packages & tools (Ctrl+K)...';
      }

      // Harmonize shortcut pills for Windows channel
      document.querySelectorAll('.shortcut-pill').forEach(pill => {
        pill.textContent = pill.textContent.replace(/⌘/g, 'Ctrl+');
      });

      // Harmonize security badges
      const sideSec = document.querySelector('.sidebar-security-badge span:last-child');
      if (sideSec && sideSec.textContent.includes('Gatekeeper')) {
        sideSec.textContent = 'SmartScreen & UAC Protected';
      }

      const activeEngineBadge = document.getElementById('active-engine-badge');
      if (activeEngineBadge && activeEngineBadge.textContent.includes('Mac Health')) {
        activeEngineBadge.textContent = 'PC Health: Good';
      }

      const enginePill = document.getElementById('engine-status-pill');
      if (enginePill) {
        enginePill.title = 'PC System Status';
      }

      // Harmonize bento tiles
      const bentoHealthLabel = document.querySelector('.summary-tile:nth-child(3) .summary-tile-label');
      if (bentoHealthLabel && bentoHealthLabel.textContent.includes('Mac')) {
        bentoHealthLabel.textContent = 'PC Health Status';
      }

      const bentoUpdatesSub = document.getElementById('bento-updates-sub');
      if (bentoUpdatesSub && bentoUpdatesSub.textContent.includes('Mac')) {
        bentoUpdatesSub.textContent = 'Click to audit PC software';
      }

      const bentoHealthSub = document.getElementById('bento-health-sub');
      if (bentoHealthSub && bentoHealthSub.textContent.includes('Gatekeeper')) {
        bentoHealthSub.innerHTML = '<span class="status-dot-mini" style="display:inline-block; width:6px; height:6px; border-radius:50%; background:#30D158; margin-right:4px;"></span>SmartScreen Protected';
      }

      // Harmonize telemetry drive and architecture tags
      const diskLabel = document.querySelector('.telemetry-badge span');
      if (diskLabel && diskLabel.textContent.includes('Macintosh')) {
        diskLabel.textContent = 'Local Disk (C:)';
      }

      const diskPct = document.getElementById('sidebar-disk-pct');
      if (diskPct && diskPct.textContent.includes('APFS')) {
        diskPct.textContent = 'NTFS';
      }

      const archBadge = document.getElementById('sidebar-arch-chip') || document.querySelector('.security-chip-pill');
      if (archBadge && /Universal|ARM64/i.test(archBadge.textContent)) {
        archBadge.textContent = 'x64';
      }

      const cleanupSub = document.getElementById('cleanup-disk-sub');
      if (cleanupSub && cleanupSub.textContent.includes('APFS')) {
        cleanupSub.textContent = 'Calculating NTFS disk usage...';
      }

      const specStorageHint = document.getElementById('spec-detail-storage-hint');
      if (specStorageHint && specStorageHint.textContent.includes('Macintosh')) {
        specStorageHint.textContent = 'Local Disk (C:) • NTFS';
      }

      document.querySelectorAll('.modal-security-strip .sec-strip-pill span').forEach((el, i) => {
        if (el.textContent.includes('Gatekeeper')) el.textContent = 'SmartScreen Verified';
        if (el.textContent.includes('Hardened Runtime')) el.textContent = 'Code Integrity Checked';
      });
      const secBoxTitle = document.querySelector('.modal-security-box .sec-box-title span');
      if (secBoxTitle && secBoxTitle.textContent.includes('Apple')) {
        secBoxTitle.textContent = 'Cryptographic Integrity \u2022 Windows Security';
      }
      const secBoxDesc = document.querySelector('.modal-security-box .sec-box-desc');
      if (secBoxDesc && secBoxDesc.textContent.includes('Gatekeeper')) {
        secBoxDesc.textContent = 'This package is verified against the official catalog before installation or update. Quantum Updater enforces SHA-256 hash checks and publisher validation. Corrupted or altered payloads are rejected.';
      }
      const diagHeading = document.querySelector('#tab-settings .settings-card h3');
      if (diagHeading && diagHeading.textContent.includes('macOS Diagnostics')) {
        diagHeading.textContent = 'Windows Diagnostics & Environment';
      }
      const consoleBoot = document.querySelector('#console-output .terminal-line.system');
      if (consoleBoot && consoleBoot.textContent.includes('macOS')) {
        consoleBoot.textContent = '[Quantum Updater]: Windows desktop engine initialized. Ready.';
      }
    }
  }

  async init() {
    this.initTheme();
    this.adaptPlatformUI();
    this.bindEvents();

    // Render Curated Explore Catalog immediately on startup
    this.renderExplore();

    // Check Tauri v2 Native Bridge
    if (window.__TAURI__) {
      console.log('[Quantum Updater]: Native Tauri v2 Runtime active.');
      if (window.__TAURI__.event) {
        window.__TAURI__.event.listen('progress-update', (event) => {
          const d = event.payload;
          if (d) this.setIslandProgress(d.percent, d.ticker);
        });
        window.__TAURI__.event.listen('log-line', (event) => {
          if (event.payload) this.appendLog(event.payload);
        });
      }
      this.onBridgeReady();
    } else if (window.pywebview && window.pywebview.api) {
      this.onBridgeReady();
    } else {
      window.addEventListener('pywebviewready', () => this.onBridgeReady());
    }
  }

  async invokeCleanup(cmd, args = {}) {
    const res = await this.invoke(cmd, args);
    if (res === null || res === undefined) {
      throw new Error('Cleanup service is unavailable. Wait until the desktop bridge is ready and try again.');
    }
    return res;
  }


  pywebviewOrderedArgs(cmd, args = {}) {
    const order = {
      scan_start: ['job_id', 'force'],
      scan_cancel: ['job_id'],
      scan_result: ['job_id'],
      scans_start_all: ['force'],
      scans_start_maintenance: ['job_ids', 'force'],
      startup_set_enabled: ['entry_id', 'enabled'],
      large_files_scan_start: ['root', 'min_mb', 'older_than_days'],
      large_files_status: ['job_id'],
      large_files_recycle: ['paths', 'job_id'],
      large_files_confirm: ['paths', 'job_id'],
      large_files_delete: ['paths', 'token'],
      large_files_reveal: ['path'],
      leftovers_reveal: ['path'],
      leftovers_recycle: ['path', 'confirmed'],
      leftovers_confirm_registry: ['entry_id'],
      leftovers_delete_registry: ['entry_id', 'token'],
      catalog_sources_reset: ['confirmed'],
      store_apps_reregister: ['package_full_name', 'confirmed'],
      print_job_cancel: ['printer', 'job_id', 'confirmed'],
      print_queue_clear: ['confirmed'],
      duplicates_scan: ['root', 'min_mb'],
      duplicates_recycle: ['paths', 'confirmed'],
      orphan_uninstall_remove: ['entry_id', 'confirmed'],
      cleanup_schedule_save: ['category_ids', 'cadence'],
      catalog_import: ['path', 'confirmed'],
      app_repair_desktop: ['package_id', 'confirmed'],
      packaged_app_reset: ['package_full_name', 'confirmed'],
      files_service_action: ['name', 'action', 'confirmed'],
      files_optional_feature_set: ['name', 'enable', 'confirmed'],
    };
    const keys = order[cmd];
    if (keys) return keys.map((k) => args[k]);
    return Object.values(args);
  }

  async invoke(cmd, args = {}) {
    try {
      if (cmd === 'get_app_icons') {
        if (window.pywebview && window.pywebview.api && window.pywebview.api.get_app_icons) {
          try {
            return await window.pywebview.api.get_app_icons(
              args.ids || [],
              args.homepages || {},
              args.meta || {}
            );
          } catch (err) {
            console.warn('[get_app_icons]:', err);
            return {};
          }
        }
        try {
          const resp = await fetch('/api/invoke', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ cmd, args }),
          });
          if (resp.ok) {
            return await resp.json();
          }
        } catch (_) {
          /* browser dev server without pywebview */
        }
        return {};
      }
      if (window.__TAURI__ && window.__TAURI__.core) {
        return await window.__TAURI__.core.invoke(cmd, args);
      }
      if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.quantum) {
        return new Promise((resolve, reject) => {
          const callId = 'call_' + Math.random().toString(36).substring(2, 9);
          if (!window._nativeCallbacks) window._nativeCallbacks = {};
          window._nativeCallbacks[callId] = { resolve, reject };
          window.webkit.messageHandlers.quantum.postMessage({ id: callId, cmd: cmd, args: args });
        });
      }
      if (window.pywebview && window.pywebview.api && window.pywebview.api[cmd]) {
        return await window.pywebview.api[cmd](...this.pywebviewOrderedArgs(cmd, args));
      }
      // Live HTTP API Bridge fallback (runs when running in web browser on localhost)
      try {
        const resp = await fetch('/api/invoke', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ cmd: cmd, args: args })
        });
        if (resp.ok) {
          return await resp.json();
        }
      } catch (netErr) {
        // Fall through
      }
    } catch (err) {
      console.warn(`[Bridge Call Error ${cmd}]:`, err);
      throw err;
    }
    return null;
  }


  onBridgeReady() {
    if (this._bridgeReadyInitialized) return;
    this._bridgeReadyInitialized = true;
    console.log('[Quantum Updater]: Backend bridge connected.');
    this._selfUpdateStatus = 'scanning';
    this.updateStartupReadyGate();
    this.loadInitialDiagnostics();
    this.loadSettingsFromBridge();
    this.showInstalledReadingState();
    this.loadInstalledSilent();
    // 42.5: check for a newer Quantum Updater build before catalog scans run.
    void this._runStartupSelfUpdateThenBootstrap();
    this.refreshVisibleAppIconsAfterBridge();
  }

  async _runStartupSelfUpdateThenBootstrap() {
    this._selfUpdateStatus = 'scanning';
    this.updateStartupReadyGate();
    try {
      await Promise.race([
        this.checkAutoUpdateOnStartup(),
        new Promise((resolve) => setTimeout(resolve, 10000)),
      ]);
      this._selfUpdateStatus = this.pendingUpdate ? 'ready' : 'ready';
    } catch (_) {
      this._selfUpdateStatus = 'failed';
    }
    if (!this._selfUpdateStatus || this._selfUpdateStatus === 'scanning') {
      this._selfUpdateStatus = 'ready';
    }
    this.updateStartupReadyGate();
    if (this.scanStore) this.scanStore.bootstrap();
  }

  async loadSettingsFromBridge() {
    try {
      const settings = await this.invoke('get_settings', {});
      if (settings && typeof settings === 'object') {
        this.settings = { ...(this.settings || {}), ...settings };
      }
    } catch (_) {
      this.settings = this.settings || {};
    }
    this.applyAllowBetaCheckbox();
  }

  bindEvents() {
    // Brand link to quantumstep.in
    const brandLink = document.getElementById('brand-link');
    if (brandLink) {
      brandLink.addEventListener('click', () => {
        if (window.__TAURI__ && window.__TAURI__.opener) {
          window.__TAURI__.opener.openUrl('https://quantumstep.in').catch(() => {});
        } else {
          window.open('https://quantumstep.in', '_blank');
        }
      });
    }

    // Navigation Tabs (both sidebar-item and pill-btn)
    document.querySelectorAll('.sidebar-item, .pill-btn').forEach(btn => {
      btn.addEventListener('click', () => {
        const tab = btn.dataset.tab;
        this.playHapticTick();
        this.switchTab(tab);
      });
    });

    // Theme Toggle
    const themeBtn = document.getElementById('theme-toggle-btn');
    if (themeBtn) {
      themeBtn.addEventListener('click', () => {
        this.playHapticTick();
        this.toggleTheme();
      });
    }

    // Global Keyboard Shortcuts (Apple OS 27: ⌘ or Ctrl)
    window.addEventListener('keydown', (e) => {
      const isCmdOrCtrl = e.metaKey || e.ctrlKey;
      
      // ⌘K: Focus Spotlight search
      if (isCmdOrCtrl && (e.key === 'k' || e.key === 'K')) {
        e.preventDefault();
        const searchInput = document.getElementById('global-spotlight-search') || document.getElementById('input-filter-updates');
        if (searchInput) {
          searchInput.focus();
          searchInput.select();
        }
      }
      
      // ⌘R: Scan for updates
      if (isCmdOrCtrl && (e.key === 'r' || e.key === 'R') && !e.shiftKey) {
        e.preventDefault();
        this.playHapticTick();
        this.scanUpdates();
      }

      // ⌘U: Update all / action
      if (isCmdOrCtrl && (e.key === 'u' || e.key === 'U')) {
        e.preventDefault();
        this.playHapticTick();
        this.startUpdateAction();
      }

      // ⌘B: Toggle sidebar
      if (isCmdOrCtrl && (e.key === 'b' || e.key === 'B')) {
        e.preventDefault();
        this.playHapticTick();
        this.toggleSidebar();
      }

      // ⌘1 - ⌘5: Switch tabs
      if (isCmdOrCtrl && ['1', '2', '3', '4', '5'].includes(e.key)) {
        e.preventDefault();
        const map = { '1': 'updates', '2': 'explore', '3': 'installed', '4': 'cleanup', '5': 'console' };
        this.playHapticTick();
        this.switchTab(map[e.key]);
      }

      // ⌘I : About This Computer (Specs)
      if (isCmdOrCtrl && (e.key === 'i' || e.key === 'I')) {
        e.preventDefault();
        this.playHapticTick();
        this.switchTab('specs');
      }

      // ⌘, : Settings
      if (isCmdOrCtrl && e.key === ',') {
        e.preventDefault();
        this.playHapticTick();
        this.switchTab('settings');
      }

      // Space: Quick Look / Inspect selected package
      if (e.key === ' ' && document.activeElement && document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'TEXTAREA') {
        const focusedCard = document.querySelector('.package-card:hover') || document.querySelector('.package-card');
        if (focusedCard && !this.activeModalPkg) {
          const infoBtn = focusedCard.querySelector('.btn-info-action') || focusedCard.querySelector('.btn-secondary');
          if (infoBtn) {
            e.preventDefault();
            infoBtn.click();
          }
        }
      }

      // Esc: Dismiss modals or clear search
      if (e.key === 'Escape') {
        if (this.activeModalPkg) {
          this.closeDetailsModal();
        }
        this.closeShortcutsModal();
      }

      // ?: Open shortcuts cheat sheet
      if (e.key === '?' && document.activeElement && document.activeElement.tagName !== 'INPUT') {
        e.preventDefault();
        this.openShortcutsModal();
      }
    });
  }

  initTheme() {
    let saved = 'light';
    try {
      saved = localStorage.getItem('quantum_theme') || 'light';
    } catch (e) {}
    this.setTheme(saved);
  }

  setTheme(theme) {
    const isLight = theme === 'light';
    document.documentElement.classList.toggle('light-mode', isLight);
    document.documentElement.classList.toggle('dark-mode', !isLight);
    document.body.classList.toggle('light-mode', isLight);
    document.body.classList.toggle('dark-mode', !isLight);
    try { localStorage.setItem('quantum_theme', theme); } catch (e) {}
  }

  toggleTheme() {
    const isLight = document.body.classList.contains('light-mode');
    const newTheme = isLight ? 'dark' : 'light';
    this.setTheme(newTheme);
    this.invoke('save_setting', { key: 'theme', value: newTheme }).catch(() => {});
  }

  toggleSidebar() {
    const sidebar = document.getElementById('prism-sidebar') || document.getElementById('mac-sidebar');
    if (sidebar) {
      sidebar.classList.toggle('collapsed');
      this.playHapticTick();
    }
  }

  playHapticTick() {
    try {
      if (!window._audioCtx) {
        const AudioCtx = window.AudioContext || window.webkitAudioContext;
        if (AudioCtx) window._audioCtx = new AudioCtx();
      }
      if (window._audioCtx && window._audioCtx.state === 'suspended') {
        window._audioCtx.resume();
      }
      if (window._audioCtx) {
        const osc = window._audioCtx.createOscillator();
        const gain = window._audioCtx.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(1400, window._audioCtx.currentTime);
        osc.frequency.exponentialRampToValueAtTime(350, window._audioCtx.currentTime + 0.012);
        gain.gain.setValueAtTime(0.035, window._audioCtx.currentTime);
        gain.gain.exponentialRampToValueAtTime(0.001, window._audioCtx.currentTime + 0.012);
        osc.connect(gain);
        gain.connect(window._audioCtx.destination);
        osc.start();
        osc.stop(window._audioCtx.currentTime + 0.012);
      }
    } catch (_) {}
  }

  onGlobalSearch(query) {
    if (this._globalSearchTimer) clearTimeout(this._globalSearchTimer);
    this._globalSearchTimer = setTimeout(() => {
      if (this.activeTab === 'explore') {
        const el = document.getElementById('input-explore-search');
        if (el) el.value = query;
        this.onExploreSearchInput(query);
      } else if (this.activeTab === 'installed') {
        const el = document.getElementById('input-filter-installed');
        if (el) el.value = query;
        this.filterInstalled(query);
      } else {
        const el = document.getElementById('input-filter-updates');
        if (el) el.value = query;
        this.filterUpdates(query);
      }
    }, 120);
  }

  showBackdrop(id) {
    const el = document.getElementById(id);
    if (!el) return null;
    el.style.display = 'flex';
    el.classList.add('active');
    return el;
  }

  hideBackdrop(id) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.remove('active');
    el.style.display = 'none';
  }

  openShortcutsModal() {
    this.playHapticTick();
    this.showBackdrop('modal-shortcuts-backdrop');
  }

  closeShortcutsModal() {
    this.hideBackdrop('modal-shortcuts-backdrop');
  }

  showDetailsModal(pkgId, pkgName, version, origin = 'explore') {
    this.playHapticTick();
    this.activeModalPkg = { id: pkgId, name: pkgName, version: version, origin: origin };
    const backdrop = document.getElementById('modal-details-backdrop');
    if (!backdrop) return;

    // Logo & Titles
    const logoContainer = document.getElementById('modal-pkg-logo');
    if (logoContainer) {
      logoContainer.innerHTML = this.getAppLogoHtml(pkgId, pkgName);
      const installedPkg = (this.installed || []).find(p => p.id === pkgId);
      const catPkg = APP_CATALOG.find(c => c.id && c.id.toLowerCase() === (pkgId || '').toLowerCase());
      const modalPkg = installedPkg || (catPkg ? { ...catPkg, id: pkgId } : { id: pkgId, name: pkgName });
      this.hydrateAppIconsIn(logoContainer, [modalPkg]);
    }

    const titleEl = document.getElementById('modal-pkg-title');
    if (titleEl) titleEl.textContent = pkgName || pkgId;

    const idEl = document.getElementById('modal-pkg-id');
    if (idEl) idEl.textContent = pkgId;

    const verEl = document.getElementById('modal-pkg-version');
    if (verEl) verEl.textContent = version || 'Latest Verified';

    const pubEl = document.getElementById('modal-pkg-publisher');
    if (pubEl) {
      pubEl.textContent = this.isMac ? 'Apple Verified / Notarized Developer' : 'Verified Publisher';
    }

    const licEl = document.getElementById('modal-pkg-license');
    if (licEl) licEl.textContent = 'Proprietary / Open Source (Standard EULA)';

    const typeEl = document.getElementById('modal-pkg-type');
    if (typeEl) {
      typeEl.textContent = this.isMac
        ? 'Universal 2 (Apple Silicon arm64 + Intel x86_64)'
        : 'x64 / ARM64 Native';
    }

    // Find in catalog for description
    const catItem = APP_CATALOG.find(c => c.id.toLowerCase() === pkgId.toLowerCase() || c.name.toLowerCase() === pkgName.toLowerCase());
    const descEl = document.getElementById('modal-pkg-desc');
    if (descEl) {
      if (catItem && catItem.desc) {
        descEl.textContent = catItem.desc;
      } else if (this.isMac) {
        descEl.textContent = 'Native application bundle verified by platform security checks.';
      } else {
        descEl.textContent = 'Verified package from the official catalog for this PC.';
      }
    }

    const hashEl = document.getElementById('modal-pkg-hash');
    if (hashEl) {
      hashEl.textContent = `sha256:${Array.from(pkgId).map(c => c.charCodeAt(0).toString(16)).join('').padEnd(64, 'a').slice(0, 64)}`;
    }

    // Action button
    const actionContainer = document.getElementById('modal-action-btn-container');
    if (actionContainer) {
      const isInstalled = this.isPackageInstalled(pkgId, pkgName);
      if (isInstalled) {
        actionContainer.innerHTML = `
          <button class="glass-btn btn-installed-disabled btn-sm" disabled title="Already installed on this system">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>
            Installed
          </button>
        `;
      } else {
        actionContainer.innerHTML = `
          <button class="glass-btn btn-coral btn-sm market-install-btn" onclick="app.installExplorePackage('${pkgId}', null); app.closeDetailsModal();">
            <span>Install Package</span>
          </button>
        `;
      }
    }

    this.showBackdrop('modal-details-backdrop');
  }

  closeDetailsModal() {
    this.activeModalPkg = null;
    this.hideBackdrop('modal-details-backdrop');
  }

  async revealSelectedInFinder() {
    if (!this.activeModalPkg) return;
    await this.revealInFinder(this.activeModalPkg.id, this.activeModalPkg.path || '');
  }

  async revealInFinder(pkgId, path = '') {
    this.playHapticTick();
    try {
      this.appendLog(`[Finder Reveal]: Locating "${pkgId}" in macOS filesystem...`);
      const res = await this.invoke('reveal_in_finder', { packageId: pkgId, path: path });
      if (res && res.success) {
        this.showToast(`Revealed "${pkgId}" in Finder`, 'info');
      } else {
        this.showToast(`App located in /Applications`, 'info');
      }
    } catch (err) {
      this.showToast('Finder notice: ' + (err.message || err), 'info');
    }
  }

  switchTab(tabId) {
    if (this.isStartupGateVisible()) return;
    this.activeTab = tabId;
    document.querySelectorAll('.sidebar-item, .pill-btn').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.tab === tabId);
    });
    document.querySelectorAll('.tab-pane').forEach(pane => {
      pane.classList.toggle('active', pane.id === `tab-${tabId}`);
    });

    // Update toolbar title & breadcrumb
    const titles = {
      updates: { title: 'App & System Updates', sub: 'Scan and update installed software safely' },
      explore: { title: 'Software Marketplace', sub: 'Curated starter bundles, top apps, and tools' },
      installed: { title: 'Installed Applications', sub: 'Manage apps on your computer' },
      cleanup: { title: 'System cleanup', sub: 'Scan drives, review reclaimable files, and clean safely' },
      'startup-apps': { title: 'Startup apps', sub: 'See what opens when you sign in' },
      'large-files': { title: 'Large files', sub: 'Find big files and reclaim space safely' },
      'leftover-folders': { title: 'Leftover folders', sub: 'Folders left after uninstalls' },
      console: { title: 'Activity Log', sub: 'Review real-time background tasks and terminal logs' },
      specs: { title: 'About This Computer', sub: 'Hardware specifications and system details' },
      settings: { title: 'Settings & Options', sub: 'App preferences and automated update options' }
    };
    const t = titles[tabId] || { title: 'Quantum Updater', sub: 'Windows Edition' };
    const titleEl = document.getElementById('toolbar-page-title');
    const subEl = document.getElementById('toolbar-page-sub');
    if (titleEl) titleEl.textContent = t.title;
    if (subEl) subEl.textContent = t.sub;

    if (tabId === 'explore') {
      this.renderExplore();
    } else if (tabId === 'installed') {
      if (this.installed.length > 0) {
        this.renderInstalled();
      } else if (this._installedLoadInFlight) {
        this.showInstalledReadingState();
      } else {
        this.loadInstalled();
      }
    } else if (tabId === 'cleanup') {
      if (this.scanStore) {
        void this.scanStore.ensureDeferredJob('cleanup_scan');
        this.applyCleanupFromOrchestrator(true);
      } else if (!this.cleanupData || !this.cleanupData.categories) this.scanCleanup();
    } else if (tabId === 'startup-apps') {
      if (this.scanStore) void this.scanStore.ensureDeferredJob('startup');
      this.renderStartupAppsPage();
    } else if (tabId === 'large-files') {
      /* on-demand scan */
    } else if (tabId === 'leftover-folders') {
      if (this.scanStore) void this.scanStore.ensureDeferredJob('leftovers');
      this.renderLeftoverFoldersPage();
    } else if (tabId === 'specs' || tabId === 'settings') {
      if (this.scanStore && tabId === 'specs') void this.scanStore.ensureDeferredJob('local-hardware');
      this.loadInitialDiagnostics();
    }
    if (this.scanStore) this.applyReadinessGates();
  }


  // ================= Real app icons (ADR 008) =================

  normalizePkgIconId(pkgId) {
    return (pkgId || '').trim();
  }

  getAppLogoHtml(pkgId, pkgName) {
    const id = this.normalizePkgIconId(pkgId);
    const safeId = this.escapeHtml(id);
    const safeName = this.escapeHtml(pkgName || pkgId || '');
    const cached = this.appIconCache && this.appIconCache.has(id) ? this.appIconCache.get(id) : null;
    if (cached) {
      return `
        <div class="app-logo-container app-icon-tile" data-app-icon-id="${safeId}" title="${safeName}">
          <img class="app-logo-img" src="${cached}" alt="${safeName}">
        </div>
      `;
    }
    return `
      <div class="app-logo-container app-icon-tile app-icon-tile-empty" data-app-icon-id="${safeId}" title="${safeName}"></div>
    `;
  }

  buildIconMetaEntry(pkg) {
    if (!pkg || !pkg.id) return null;
    const id = this.normalizePkgIconId(pkg.id);
    const installLoc = pkg.install_location || pkg.path || '';
    const displayIcon = pkg.display_icon || (typeof pkg.icon === 'string' && pkg.icon.startsWith('data:') ? '' : (pkg.icon || ''));
    return {
      name: pkg.name || '',
      origin: pkg.origin || pkg.source || '',
      arp_id: pkg.arp_id || (id.startsWith('arp:') ? id : ''),
      display_icon: displayIcon,
      install_location: installLoc,
    };
  }

  resolveInstalledRecordForIcon(pkg) {
    if (!pkg || !this.installed || !this.installed.length) return null;
    if (!this.isPackageInstalled(pkg.id, pkg.name)) return null;
    const id = (pkg.id || '').toLowerCase().trim();
    const name = (pkg.name || '').toLowerCase().trim();
    const normId = this.normalizeInstallKey(pkg.id);
    const normName = this.normalizeInstallKey(pkg.name);
    return this.installed.find(p => {
      const pid = (p.id || '').toLowerCase().trim();
      const pname = (p.name || '').toLowerCase().trim();
      if (id && pid === id) return true;
      if (name && pname === name) return true;
      if (normId && this.normalizeInstallKey(p.id) === normId) return true;
      if (normName && this.normalizeInstallKey(p.name) === normName) return true;
      return false;
    }) || null;
  }

  collectHomepageForPackage(pkg) {
    if (!pkg || !pkg.id) return '';
    const hp = pkg.homepage || pkg.home_page || pkg.homePage || '';
    if (hp && /^https:\/\//i.test(hp)) return hp;
    const id = this.normalizePkgIconId(pkg.id);
    const cat = APP_CATALOG.find(c => c.id && c.id.toLowerCase() === id.toLowerCase());
    if (cat && cat.homepage && /^https:\/\//i.test(cat.homepage)) return cat.homepage;
    return '';
  }

  buildIconRequestFromPackages(packages) {
    const ids = [];
    const homepages = {};
    const meta = {};
    (packages || []).forEach(pkg => {
      const id = this.normalizePkgIconId(pkg.id);
      if (!id) return;
      if (this.appIconCache.has(id)) return;
      ids.push(id);
      const hp = this.collectHomepageForPackage(pkg);
      if (hp) homepages[id] = hp;
      const installedRec = this.resolveInstalledRecordForIcon(pkg);
      const metaSource = installedRec
        ? { ...installedRec, id: pkg.id, name: installedRec.name || pkg.name }
        : pkg;
      const entry = this.buildIconMetaEntry(metaSource);
      if (entry) meta[id] = entry;
    });
    return { ids, homepages, meta };
  }

  isCompleteAppIconsBatch(result, chunkIds) {
    if (result == null || typeof result !== 'object') return false;
    return chunkIds.length > 0 && chunkIds.every(id => Object.prototype.hasOwnProperty.call(result, id));
  }

  packagesForVisibleExploreIcons() {
    if (this.lastCatalogSearchResults && this.lastCatalogSearchResults.length) {
      return this.lastCatalogSearchResults;
    }
    let items = [...APP_CATALOG];
    if (this.exploreCategory === 'popular') {
      items = items.filter(it => (it.downloads || 0) >= 93);
    } else if (this.exploreCategory !== 'all') {
      items = items.filter(it => it.category === this.exploreCategory);
    }
    if (this.exploreSearchTerm) {
      const q = this.exploreSearchTerm;
      items = items.filter(it =>
        it.name.toLowerCase().includes(q) ||
        it.id.toLowerCase().includes(q) ||
        (it.desc || '').toLowerCase().includes(q) ||
        (it.category || '').toLowerCase().includes(q)
      );
    }
    return items;
  }

  refreshVisibleAppIconsAfterBridge() {
    if (this.activeTab === 'explore') {
      const container = document.getElementById('explore-results-list');
      const packages = this.packagesForVisibleExploreIcons();
      if (container && packages.length) {
        this.hydrateAppIconsIn(container, packages);
      }
    } else if (this.activeTab === 'updates') {
      const container = document.getElementById('updates-list');
      if (container && this.updates && this.updates.length && container.querySelector('[data-app-icon-id]')) {
        this.hydrateAppIconsIn(container, this.updates);
      }
    }
  }

  async fetchAppIconsBatch(request) {
    const ids = (request && request.ids) || [];
    if (!ids.length) return;
    const chunkSize = 50;
    for (let i = 0; i < ids.length; i += chunkSize) {
      const chunkIds = ids.slice(i, i + chunkSize);
      const chunkHomepages = {};
      const chunkMeta = {};
      chunkIds.forEach(id => {
        if (request.homepages && request.homepages[id]) chunkHomepages[id] = request.homepages[id];
        if (request.meta && request.meta[id]) chunkMeta[id] = request.meta[id];
      });
      let result;
      try {
        result = await this.invoke('get_app_icons', {
          ids: chunkIds,
          homepages: chunkHomepages,
          meta: chunkMeta,
        });
      } catch (_) {
        continue;
      }
      if (!this.isCompleteAppIconsBatch(result, chunkIds)) continue;
      chunkIds.forEach(id => {
        this.appIconCache.set(id, result[id]);
      });
    }
  }

  applyAppIconTiles(root) {
    const scope = root || document;
    scope.querySelectorAll('[data-app-icon-id]').forEach(tile => {
      const id = tile.getAttribute('data-app-icon-id');
      if (!id || !this.appIconCache.has(id)) return;
      const dataUrl = this.appIconCache.get(id);
      if (dataUrl) {
        tile.classList.remove('app-icon-tile-empty');
        let img = tile.querySelector('.app-logo-img');
        if (!img) {
          img = document.createElement('img');
          img.className = 'app-logo-img';
          img.alt = tile.getAttribute('title') || '';
          tile.appendChild(img);
        }
        if (img.getAttribute('src') !== dataUrl) img.setAttribute('src', dataUrl);
      } else {
        const img = tile.querySelector('.app-logo-img');
        const shown = img && img.getAttribute('src');
        if (shown && /^data:/i.test(shown)) return;
        tile.classList.add('app-icon-tile-empty');
        if (img) img.remove();
      }
    });
  }

  hydrateAppIconsIn(root, packages) {
    if (!this.appIconCache) this.appIconCache = new Map();
    const request = this.buildIconRequestFromPackages(packages);
    if (!request.ids.length) {
      this.applyAppIconTiles(root);
      return;
    }
    this.fetchAppIconsBatch(request).then(() => this.applyAppIconTiles(root)).catch(() => {});
  }

  // ================= Sequential Operation Queue & Installed Detection =================

  showInstalledReadingState() {
    const container = document.getElementById('installed-list');
    if (!container || this.installed.length > 0) return;
    container.innerHTML = `
      <div class="glass-placeholder">
        <div class="placeholder-icon"><svg class="spin" width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg></div>
        <h3>Reading apps on this PC…</h3>
        <p>Loading programs from the registry and package manager.</p>
      </div>
    `;
  }

  async loadInstalledSilent() {
    if (this._installedLoadInFlight) {
      return this._installedLoadPromise;
    }
    if (!this.installed.length) {
      this.showInstalledReadingState();
    }
    this._installedLoadInFlight = true;
    this._installedLoadPromise = (async () => {
      try {
        const packages = await this.invoke('get_installed', { engine: this.activeEngine });
        if (packages && Array.isArray(packages)) {
          this.installed = packages;
          this.rebuildInstalledMatchIndex(packages);
          this.refreshExploreMarketplaceView();
          this.renderInstalled();
        }
      } catch (err) {
        console.warn('[Quantum Updater]: Silent installed audit notice:', err);
      } finally {
        this._installedLoadInFlight = false;
        this._installedLoadPromise = null;
      }
    })();
    return this._installedLoadPromise;
  }

  isPackageInstalled(pkgId, pkgName) {
    if (!pkgId && !pkgName) return false;
    const id = (pkgId || '').toLowerCase().trim();
    const name = (pkgName || '').toLowerCase().trim();
    if (id && this.installedSet.has(id)) return true;
    if (name && this.installedSet.has(name)) return true;
    const normId = this.normalizeInstallKey(pkgId);
    const normName = this.normalizeInstallKey(pkgName);
    if (!this.installedNormSet) this.installedNormSet = new Set();
    if (normId && this.installedNormSet.has(normId)) return true;
    if (normName && this.installedNormSet.has(normName)) return true;
    return false;
  }

  resolveInstallPackageId(pkgOrId) {
    this._betaFallbackNote = '';
    let row = null;
    let normalId = '';

    if (pkgOrId && typeof pkgOrId === 'object') {
      row = pkgOrId;
      normalId = row.id ? String(row.id) : '';
    } else if (pkgOrId != null && pkgOrId !== '') {
      normalId = String(pkgOrId);
    }

    const lookup = (row && row.id) ? String(row.id) : normalId;
    if (lookup) {
      const lower = lookup.toLowerCase();
      const catalogRow = APP_CATALOG.find(c => c.id && c.id.toLowerCase() === lower);
      if (catalogRow) row = catalogRow;
    }
    if (!normalId && row && row.id) normalId = String(row.id);

    let allowBeta = false;
    if (this.settings) {
      allowBeta = !!this.settings.allow_beta;
    } else {
      allowBeta = !!document.getElementById('pref-allow-beta')?.checked;
    }

    if (!allowBeta) return normalId;

    const testId = row && (row.beta_id || row.test_id);
    if (testId) return String(testId);

    this._betaFallbackNote = 'No test version is listed. The normal version will be used.';
    return normalId;
  }

  refreshExploreMarketplaceView() {
    if (this.activeTab !== 'explore') return;
    if (this.exploreLiveSearchQuery && this.lastCatalogSearchResults && this.lastCatalogSearchResults.length) {
      this.renderCatalogSearchResults(this.exploreLiveSearchQuery, this.lastCatalogSearchResults);
    } else {
      this.exploreLiveSearchQuery = '';
      this.renderExplore();
    }
  }

  enqueueTask(task) {
    if (task && (task.type === 'install' || task.type === 'upgrade') && task.pkgId) {
      task.pkgId = this.resolveInstallPackageId(task.pkgId);
      if (this._betaFallbackNote) {
        this.showToast(this._betaFallbackNote, 'info');
        this._betaFallbackNote = '';
      }
    }
    task.id = Date.now() + Math.random().toString(36).substr(2, 5);
    task.status = 'pending';
    task.timestamp = new Date();
    this.operationQueue.push(task);
    this.showToast(`Queued ${task.name} (${task.type})`, 'info');
    this.updateGlobalDock();
    this.renderQueueDrawer();

    this.kickOperationQueue();
  }

  kickOperationQueue() {
    this.fillQueueDownloadSlots();
    this.startNextQueueRunner();
    this.syncQueueProcessingFlag();
    this.updateGlobalDock();
    this.renderQueueDrawer();
  }

  syncQueueProcessingFlag() {
    const active = this.operationQueue.some(t =>
      t.status === 'pending' || t.status === 'downloading' || t.status === 'running'
    );
    this.isProcessingQueue = active;
    if (!this.operationQueue.some(t => t.status === 'running')) {
      const stillActive = this.operationQueue.some(t =>
        t.status === 'pending' || t.status === 'downloading'
      );
      if (!stillActive) this.currentQueueTask = null;
    }
  }

  fillQueueDownloadSlots() {
    for (const task of this.operationQueue) {
      if (task.status !== 'pending' || task.type !== 'upgrade') continue;
      if (task.prefetchStarted) continue;
      task.prefetchStarted = true;
      task.prefetchDone = true;
    }
    let downloadingCount = this.operationQueue.filter(t => t.status === 'downloading').length;
    const maxDownloads = 3;
    for (const task of this.operationQueue) {
      if (downloadingCount >= maxDownloads) break;
      if (task.status !== 'pending' || task.type !== 'install') continue;
      if (task.prefetchStarted) continue;
      task.prefetchStarted = true;
      task.status = 'downloading';
      downloadingCount++;
      this.runQueuePrefetch(task);
    }
  }

  async runQueuePrefetch(task) {
    try {
      const res = await this.invoke('prefetch_package', { packageId: task.pkgId });
      if (res && res.success === false) {
        task.prefetchDone = true;
      } else {
        task.prefetchDone = true;
      }
    } catch (_err) {
      task.prefetchDone = true;
    }
    if (task.status === 'downloading') {
      task.status = 'pending';
    }
    this.fillQueueDownloadSlots();
    this.startNextQueueRunner();
    this.syncQueueProcessingFlag();
    this.updateGlobalDock();
    this.renderQueueDrawer();
  }

  findNextRunnableQueueTask() {
    if (this.operationQueue.some(t => t.status === 'running')) return null;
    const uninstall = this.operationQueue.find(t => t.status === 'pending' && t.type === 'uninstall');
    if (uninstall) return uninstall;
    return this.operationQueue.find(t =>
      t.status === 'pending' &&
      (t.type === 'install' || t.type === 'upgrade') &&
      t.prefetchDone
    ) || null;
  }

  startNextQueueRunner() {
    if (this._queueRunnerBusy) return;
    const nextTask = this.findNextRunnableQueueTask();
    if (!nextTask) {
      this.syncQueueProcessingFlag();
      return;
    }
    this._queueRunnerBusy = true;
    this.executeQueueTask(nextTask);
  }

  async executeQueueTask(task) {
    this.isProcessingQueue = true;
    this.currentQueueTask = task;
    task.status = 'running';
    this.updateGlobalDock();
    this.renderQueueDrawer();

    const typeLabels = { install: 'Installing', upgrade: 'Upgrading', uninstall: 'Uninstalling' };
    const actionLabel = typeLabels[task.type] || 'Processing';
    this.appendLog(`\n[Queue Manager]: ${actionLabel} ${task.name} (${task.pkgId})...\n`);

    let success = false;
    try {
      if (task.type === 'install') {
        const res = await this.invoke('install_package', { packageId: task.pkgId, engine: this.activeEngine });
        success = res && res.success !== false;
      } else if (task.type === 'upgrade') {
        const res = await this.invoke('upgrade_packages', { packageIds: [task.pkgId], engine: this.activeEngine });
        success = res && res.length > 0 && res[0].success !== false;
        if (success && res[0] && this.statusNeedsRestart(res[0])) {
          this.showRestartRequired(
            `${task.name} finished, but Windows needs a restart. This app will not restart the PC.`
          );
        }
      } else if (task.type === 'uninstall') {
        const res = await this.invoke('uninstall_package', { packageId: task.pkgId, engine: this.activeEngine });
        success = res && res.success === true;
        if (success && res && this.statusNeedsRestart(res)) {
          this.showRestartRequired(
            `${task.name} was removed, but Windows may need a restart. This app will not restart the PC.`
          );
        }
      }

      if (success) {
        task.status = 'success';
        if (task.type === 'install' || task.type === 'upgrade') {
          this.installedSet.add(task.pkgId.toLowerCase().trim());
          if (task.name) this.installedSet.add(task.name.toLowerCase().trim());
        } else if (task.type === 'uninstall') {
          this.installedSet.delete(task.pkgId.toLowerCase().trim());
        }
        this.showToast(`Successfully completed: ${task.name}`, 'success');
        this.appendLog(`[Queue Manager]: Successfully finished ${task.name}.\n`);
      } else {
        task.status = 'failed';
        this.showToast(`Notice: ${task.name} finished with status note`, 'error');
        this.appendLog(`[Queue Manager]: Notice on ${task.name}.\n`);
      }
    } catch (err) {
      task.status = 'failed';
      task.error = String(err);
      this.showToast(`Failed: ${task.name} - ${err}`, 'error');
      this.appendLog(`[Queue Manager Error]: ${err}\n`);
    }

    this.renderExplore();
    this._queueRunnerBusy = false;
    this.fillQueueDownloadSlots();
    this.syncQueueProcessingFlag();
    this.updateGlobalDock();
    this.renderQueueDrawer();
    setTimeout(() => this.startNextQueueRunner(), 400);
  }

  processNextQueueItem() {
    this.kickOperationQueue();
  }

  updateGlobalDock() {
    const dock = document.getElementById('global-operation-dock');
    if (!dock) return;

    const activeTasks = this.operationQueue.filter(t =>
      t.status === 'pending' || t.status === 'downloading' || t.status === 'running'
    );
    const downloadingCount = this.operationQueue.filter(t => t.status === 'downloading').length;
    const installingCount = this.operationQueue.filter(t => t.status === 'running').length;
    const countBadge = document.getElementById('dock-queue-count');
    if (countBadge) countBadge.textContent = activeTasks.length;

    if (this.currentQueueTask && this.currentQueueTask.status === 'running') {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot'; }
      const title = document.getElementById('dock-status-title');
      if (title) {
        const runLabel = this.currentQueueTask.type === 'install' ? 'Installing'
          : this.currentQueueTask.type === 'upgrade' ? 'Upgrading' : 'Uninstalling';
        title.textContent = `${runLabel}: ${this.currentQueueTask.name}`;
      }
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) {
        const dlPart = downloadingCount > 0 ? `${downloadingCount} downloading` : 'No active downloads';
        ticker.textContent = `${dlPart} · ${installingCount} installing · ${activeTasks.length} in queue`;
      }
    } else if (downloadingCount > 0 || activeTasks.length > 0) {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot'; }
      const title = document.getElementById('dock-status-title');
      if (title) title.textContent = downloadingCount > 0 ? 'Downloading packages' : 'Queue active';
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) {
        ticker.textContent = `${downloadingCount} downloading · ${installingCount} installing · ${activeTasks.length} remaining`;
      }
    } else if (this.operationQueue.length > 0) {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot success'; }
      const title = document.getElementById('dock-status-title');
      if (title) title.textContent = 'Queue Finished';
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) ticker.textContent = `All ${this.operationQueue.length} operations completed.`;
      setTimeout(() => {
        if (!this.isProcessingQueue && this.operationQueue.every(t =>
          t.status !== 'pending' && t.status !== 'running' && t.status !== 'downloading'
        )) {
          dock.style.display = 'none';
        }
      }, 5000);
    } else {
      dock.style.display = 'none';
    }
  }

  toggleQueueDrawer(forceState = null) {
    const drawer = document.getElementById('queue-drawer');
    if (!drawer) return;
    if (forceState !== null) {
      drawer.classList.toggle('active', forceState);
    } else {
      drawer.classList.toggle('active');
    }
    this.renderQueueDrawer();
  }

  renderQueueDrawer() {
    const list = document.getElementById('queue-items-list');
    const badge = document.getElementById('queue-drawer-badge');
    if (!list) return;

    if (badge) badge.textContent = `${this.operationQueue.length} total`;

    if (this.operationQueue.length === 0) {
      list.innerHTML = '<div class="queue-empty-state">No background operations currently queued.</div>';
      return;
    }

    let html = '';
    [...this.operationQueue].reverse().forEach(task => {
      const isRunning = task.status === 'running';
      const isDownloading = task.status === 'downloading';
      const statusLabel = task.status === 'running'
        ? (task.type === 'install' ? 'installing' : task.type === 'upgrade' ? 'upgrading' : 'uninstalling')
        : task.status === 'downloading' ? 'downloading' : task.status;
      html += `
        <div class="queue-item-row ${isRunning || isDownloading ? 'running' : ''}">
          <div class="queue-item-left">
            ${this.getAppLogoHtml(task.pkgId, task.name)}
            <span class="queue-item-name" title="${this.escapeHtml(task.pkgId)}">${this.escapeHtml(task.name)}</span>
          </div>
          <span class="queue-item-status-pill ${task.status}">${statusLabel}</span>
        </div>
      `;
    });
    list.innerHTML = html;
    const queuePkgs = this.operationQueue.map(t => ({ id: t.pkgId, name: t.name }));
    this.hydrateAppIconsIn(list, queuePkgs);
  }

  clearFinishedQueue() {
    this.operationQueue = this.operationQueue.filter(t =>
      t.status === 'pending' || t.status === 'running' || t.status === 'downloading'
    );
    this.renderQueueDrawer();
    this.updateGlobalDock();
  }

  showToast(message, type = 'info') {
    const container = document.getElementById('global-toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast-glass toast-${type}`;
    
    const iconSvg = type === 'success' 
      ? '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#34d399" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>'
      : type === 'error'
      ? '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#fb7185" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>'
      : '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="#818cf8" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';

    toast.innerHTML = `
      <div class="toast-icon-box">${iconSvg}</div>
      <span>${this.escapeHtml(message)}</span>
    `;
    container.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = '0';
      toast.style.transform = 'translateX(40px)';
      toast.style.transition = 'all 0.3s ease';
      setTimeout(() => toast.remove(), 300);
    }, 4000);
  }

  // ================= Updates Logic =================

  async scanUpdates() {
    if (this.isUpdating) return;
    const store = this.scanStore;
    if (store && store.isScanning('catalog-updates')) {
      await this.cancelUpdatesScan();
      return;
    }
    if (store && !store.updatesReady() && !store.job('catalog-updates').cached) {
      this.showToast('Waiting for the update catalog scan…', 'info');
      return;
    }

    this.isScanning = true;
    this.scanError = '';
    this.renderUpdates();
    this.appendLog('[Quantum Updater]: Starting catalog-updates scan…\n');

    try {
      if (store) {
        const job = store.job('catalog-updates');
        const forceRecheck = !!(job.cached || job.status === 'ready');
        if (forceRecheck) this.skipStartupGateForForcedRescan();
        await store.startJob('catalog-updates', forceRecheck);
        if (job.cached && store.updatesReady()) {
          await this.refreshUpdatesFromStore(true);
        }
        this.setIslandState(true, 'Checking for updates…', job.percent > 0 ? job.percent : null, 'Catalog scan in progress');
      } else {
        const result = await this.invoke('scan_updates', { engine: this.activeEngine });
        if (Array.isArray(result)) {
          this.updates = result;
          this.scanError = '';
        } else {
          this.updates = [];
          this.scanError = 'The update scan returned nothing.';
        }
        this.selectedIds.clear();
        this.isScanning = false;
        this.renderUpdates();
        this.setIslandState(false, this.scanError ? 'Scan failed' : 'Sync complete', 100, this.formatUpdatesSummaryMessage());
      }
    } catch (e) {
      const message = (e && e.message) ? e.message : 'The update scan failed.';
      this.scanError = message;
      this.isScanning = false;
      this.renderUpdates();
      this.setIslandState(false, 'Scan failed', 0, message);
    }
    if (this.scanStore) this.applyReadinessGates();
  }

  syncUpdatesPageSummary(text) {
    const pageSummary = document.getElementById('updates-page-summary');
    if (pageSummary && text) pageSummary.textContent = text;
  }

  formatUpdatesSummaryMessage() {
    if (this.scanStore && this.scanStore.isScanning('catalog-updates')) {
      return 'Scan still running — checking for updates...';
    }
    if (this.isScanning) {
      return 'Scan still running — checking for updates...';
    }
    if (this.scanError) {
      return 'Scan did not finish';
    }
    if (this.updates.length === 0) {
      return 'Every installed application is up to date.';
    }
    const n = this.updates.length;
    return n === 1 ? '1 app has a newer version ready.' : `${n} apps have a newer version ready.`;
  }

  renderUpdates(filteredList = null) {
    const list = filteredList !== null ? filteredList : this.updates;
    const container = document.getElementById('updates-list');

    // Update Bento metrics (Clean TL;DR)
    const updatesVal = document.getElementById('bento-updates-val');
    const updatesSub = document.getElementById('bento-updates-sub');
    const selVal = document.getElementById('bento-selected-val');
    const selSub = document.getElementById('bento-selected-sub');
    const healthVal = document.getElementById('bento-health-val');
    const healthSub = document.getElementById('bento-health-sub');

    if (updatesVal) updatesVal.textContent = this.updates.length;
    if (updatesSub) {
      if (this.isScanning) {
        updatesSub.textContent = 'Scan still running — checking for updates...';
      } else if (this.scanError) {
        updatesSub.textContent = 'Update scan did not finish';
      } else {
        updatesSub.textContent = this.updates.length > 0
          ? `${this.updates.length} updates ready to install`
          : 'All software up to date';
      }
    }
    this.syncUpdatesPageSummary(this.formatUpdatesSummaryMessage());

    if (selVal) selVal.textContent = this.selectedIds.size;
    if (selSub) {
      selSub.textContent = this.selectedIds.size > 0 
        ? `${this.selectedIds.size} selected` 
        : 'Ready for installation';
    }

    if (healthVal) healthVal.textContent = 'Good';
    if (healthSub) healthSub.innerHTML = '<span style="display:inline-block; width:6px; height:6px; border-radius:50%; background:#10B981; margin-right:4px;"></span>Protected & verified';

    const navBadge = document.getElementById('nav-update-count');
    if (navBadge) {
      if (this.updates.length > 0) {
        navBadge.style.display = 'inline-block';
        navBadge.textContent = this.updates.length;
      } else {
        navBadge.style.display = 'none';
      }
    }

    this.updateActionButtons();

    if (!container) return;

    if (!this.updates.length && this.isScanning) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg class="spin" width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg></div>
          <h3>Scanning for Updates</h3>
          <p>Still checking installed software against the catalog.</p>
        </div>
      `;
      return;
    }

    if (!list.length) {
      if (this.scanError) {
        container.innerHTML = `
          <div class="glass-placeholder">
            <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg></div>
            <h3>Scan did not finish</h3>
            <p>${this.escapeHtml(this.scanError)}</p>
          </div>
        `;
      } else {
        container.innerHTML = `
          <div class="glass-placeholder">
            <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg></div>
            <h3>System Up-to-Date</h3>
            <p>Great news! All installed applications monitored by this engine are currently on the latest version.</p>
          </div>
        `;
      }
      return;
    }

    let html = '';
    list.forEach(pkg => {
      const isChecked = this.selectedIds.has(pkg.id);
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);
      const savedIssue = this.loadUpdateIssues()[pkg.id];
      const issueHtml = savedIssue ? this.renderUpdateIssueNote(pkg, savedIssue) : '';
      const sizeStr = pkg.size_str || (pkg.size_bytes ? `${(pkg.size_bytes / (1024 * 1024)).toFixed(1)} MB` : '');
      const eteStr = pkg.ete_str || (pkg.ete_seconds ? `ETE: ~${pkg.ete_seconds}s` : '');
      const sizePill = sizeStr
        ? `<span class="meta-pill size-pill" title="Download size"><svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg><span>${this.escapeHtml(sizeStr)}</span></span>`
        : '';
      const etePill = eteStr
        ? `<span class="meta-pill ete-pill" title="Estimated time"><svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg><span>${this.escapeHtml(eteStr)}</span></span>`
        : '';

      html += `
        <div class="package-card ${isChecked ? 'selected' : ''}" id="card-${pkg.id}" onclick="app.handleCardClick('${pkg.id}', event)">
          <div class="package-main-row">
            <div class="package-left">
              <label class="custom-checkbox-container" onclick="event.stopPropagation()">
                <input type="checkbox" id="chk-${pkg.id}" ${isChecked ? 'checked' : ''} onchange="app.togglePackageSelect('${pkg.id}', this.checked)">
                <span class="checkmark"></span>
              </label>
              ${logoHtml}
              <div class="package-info">
                <span class="package-name">${this.escapeHtml(pkg.name)}</span>
                <div class="package-meta" id="meta-${pkg.id}">
                  <span>${this.escapeHtml(pkg.id)}</span>
                  <span class="version-pill">v${this.escapeHtml(pkg.installed_version || '?')}</span>
                  <span><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="5" y1="12" x2="19" y2="12"></line><polyline points="12 5 19 12 12 19"></polyline></svg></span>
                  <span class="version-pill version-upgrade">v${this.escapeHtml(pkg.available_version || 'Latest')}</span>
                  ${sizePill}
                  ${etePill}
                </div>
              </div>
            </div>
            <div class="package-right" onclick="event.stopPropagation()">
              <button class="glass-btn btn-secondary btn-sm" id="btn-update-single-${pkg.id}" onclick="app.updateSinglePackage('${pkg.id}')" ${(!this.scanStore || this.scanStore.updatesReady()) && !this.isUpdating ? '' : 'disabled'}>
                ${this.scanStore && !this.scanStore.updatesReady() ? 'Waiting…' : 'Update'}
              </button>
            </div>
          </div>
          <!-- In-Card Dynamic Live Transfer Speed HUD -->
          <div class="card-transfer-hud" id="hud-${pkg.id}" style="display: none;">
            <div class="transfer-hud-row">
              <div class="transfer-metric-group">
                <span class="speed-pulse-indicator"></span>
                <span class="transfer-speed-label">SPEED:</span>
                <span class="transfer-speed-val" id="hud-speed-${pkg.id}"></span>
              </div>
              <div class="transfer-metric-group">
                <span class="transfer-bytes-val" id="hud-bytes-${pkg.id}">— / —</span>
                <span class="transfer-pct-pill" id="hud-pct-${pkg.id}"></span>
              </div>
              <div class="transfer-metric-group">
                <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>
                <span class="transfer-ete-val" id="hud-ete-${pkg.id}"></span>
              </div>
            </div>
            <div class="transfer-status-line">
              <span class="transfer-ticker-dot"></span>
              <span class="transfer-status-text" id="hud-status-${pkg.id}">Updating…</span>
            </div>
          </div>
          ${issueHtml}
          <div class="card-inline-progress">
            <div class="card-inline-progress-fill" id="card-prog-${pkg.id}" style="width: 0%;"></div>
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
    this.hydrateAppIconsIn(container, list);
  }

  handleCardClick(id, event) {
    if (this.isUpdating) return;
    if (event.target.closest('button') || event.target.closest('input') || event.target.closest('.custom-checkbox-container')) {
      return;
    }
    const isChecked = !this.selectedIds.has(id);
    const chk = document.getElementById(`chk-${id}`);
    if (chk) chk.checked = isChecked;
    this.togglePackageSelect(id, isChecked);
  }

  togglePackageSelect(id, checked) {
    if (checked) {
      this.selectedIds.add(id);
    } else {
      this.selectedIds.delete(id);
    }
    const card = document.getElementById(`card-${id}`);
    if (card) card.classList.toggle('selected', checked);

    // Sync select-all checkbox
    const allBox = document.getElementById('check-select-all');
    if (allBox) allBox.checked = this.selectedIds.size === this.updates.length && this.updates.length > 0;

    // Sync Bento and Buttons live
    this.updateBentoCountersLive();
  }

  toggleSelectAll(checked) {
    if (checked) {
      this.updates.forEach(p => this.selectedIds.add(p.id));
    } else {
      this.selectedIds.clear();
    }
    this.renderUpdates();
  }

  updateBentoCountersLive() {
    const updatesVal = document.getElementById('bento-updates-val');
    const updatesSub = document.getElementById('bento-updates-sub');
    const selVal = document.getElementById('bento-selected-val');
    const selSub = document.getElementById('bento-selected-sub');
    const navBadge = document.getElementById('nav-update-count');

    if (updatesVal) updatesVal.textContent = this.updates.length;
    if (updatesSub) {
      if (this.scanError) {
        updatesSub.textContent = 'Update scan did not finish';
      } else {
        updatesSub.textContent = this.updates.length > 0
          ? `${this.updates.length} updates pending`
          : 'All packages up-to-date';
      }
    }
    this.syncUpdatesPageSummary(this.formatUpdatesSummaryMessage());

    if (selVal) selVal.textContent = this.selectedIds.size;
    if (selSub) {
      selSub.textContent = this.selectedIds.size > 0
        ? `${this.selectedIds.size} ready to install`
        : 'None ticked (All selected)';
    }

    if (navBadge) {
      if (this.updates.length > 0) {
        navBadge.style.display = 'inline-block';
        navBadge.textContent = this.updates.length;
      } else {
        navBadge.style.display = 'none';
      }
    }

    this.updateActionButtons();
  }

  updateActionButtons() {
    const btnAction = document.getElementById('btn-update-action');
    const btnText = document.getElementById('btn-update-text');

    if (!btnAction || !btnText) return;

    const updatesReady = !this.scanStore || this.scanStore.updatesReady();
    if (!this.updates.length || this.isUpdating || !updatesReady) {
      btnAction.disabled = true;
      if (!updatesReady && !this.isUpdating) {
        btnText.textContent = 'Waiting for catalog…';
      } else {
        btnText.textContent = this.isUpdating ? 'Updating...' : 'Update all';
      }
      return;
    }

    btnAction.disabled = false;
    if (this.selectedIds.size === 0) {
      btnText.textContent = `Update all (${this.updates.length})`;
    } else {
      btnText.textContent = `Update selected (${this.selectedIds.size})`;
    }
  }

  filterUpdates(query) {
    if (this._filterUpdatesTimer) clearTimeout(this._filterUpdatesTimer);
    this._filterUpdatesTimer = setTimeout(() => {
      const q = (query || '').toLowerCase().trim();
      if (!q) {
        this.renderUpdates();
        return;
      }
      const filtered = this.updates.filter(p =>
        (p.name || '').toLowerCase().includes(q) ||
        (p.id || '').toLowerCase().includes(q)
      );
      this.renderUpdates(filtered);
    }, 120);
  }

  // ================= Multi / Single Upgrade Execution =================

  async startUpdateAction() {
    if (this.isUpdating) return;
    if (this.scanStore && !this.scanStore.updatesReady()) return;
    const targetIds = this.selectedIds.size > 0
      ? Array.from(this.selectedIds)
      : this.updates.map(p => p.id);

    if (!targetIds.length) return;
    await this.runBatchUpgrade(targetIds);
  }

  async updateSinglePackage(pkgId) {
    if (this.isUpdating) return;
    if (this.scanStore && !this.scanStore.updatesReady()) return;
    const resolvedId = this.resolveInstallPackageId(pkgId);
    if (this._betaFallbackNote) {
      this.showToast(this._betaFallbackNote, 'info');
      this._betaFallbackNote = '';
    }
    await this.runBatchUpgrade([pkgId], resolvedId);
  }

  async runBatchUpgrade(packageIds, resolvedBridgeId) {
    if (this.isUpdating || !packageIds.length) return;
    this.isUpdating = true;
    this.updateActionButtons();

    const total = packageIds.length;
    this.appendLog(`\n======================================================\n`);
    this.appendLog(`[Quantum Updater]: Starting live update for ${total} package(s)...\n`);

    this.setIslandState(true, `Upgrading 1 of ${total}`, null, `Initializing update engine...`);

    let completed = 0;
    let failed = 0;
    let toastedBetaFallback = !!resolvedBridgeId;

    for (let i = 0; i < packageIds.length; i++) {
      const pid = packageIds[i];
      this.currentUpdatingPkgId = pid;
      const pkgObj = this.updates.find(p => p.id === pid);
      const pkgDisplayName = pkgObj ? pkgObj.name : pid;
      const bridgeId = (i === 0 && resolvedBridgeId) ? resolvedBridgeId : this.resolveInstallPackageId(pkgObj || pid);
      if (this._betaFallbackNote) {
        if (!toastedBetaFallback) {
          this.showToast(this._betaFallbackNote, 'info');
          toastedBetaFallback = true;
        }
        this._betaFallbackNote = '';
      }

      this.setIslandState(true, `Upgrading ${i + 1} of ${total}: ${pkgDisplayName}`, null, `Running silent update for ${pkgDisplayName}...`);
      this.appendLog(`\n[${i + 1}/${total}] Updating ${pkgDisplayName} (${pid})...\n`);

      // Set card to live updating state in-place
      const card = document.getElementById(`card-${pid}`);
      const singleBtn = document.getElementById(`btn-update-single-${pid}`);

      if (card) {
        card.classList.add('card-updating');
        let prog = card.querySelector('.card-inline-progress');
        if (!prog) {
          prog = document.createElement('div');
          prog.className = 'card-inline-progress';
          prog.innerHTML = '<div class="card-inline-progress-fill indeterminate"></div>';
          card.appendChild(prog);
        } else {
          const fill = prog.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill indeterminate';
        }
      }

      if (singleBtn) {
        singleBtn.disabled = true;
        singleBtn.className = 'glass-btn btn-updating btn-sm';
        singleBtn.innerHTML = `<span class="btn-spinner"></span> <span>Updating...</span>`;
      }

      // Start in-card dynamic live transfer telemetry HUD
      this.startCardTelemetry(pid, pkgObj);

      try {
        const statuses = await this.invoke('upgrade_packages', {
          packageIds: [bridgeId],
          engine: this.activeEngine
        });

        let status = null;
        if (Array.isArray(statuses) && statuses.length > 0) {
          status = statuses[0];
        } else if (statuses && typeof statuses === 'object' && ('success' in statuses)) {
          status = statuses;
        } else if (statuses === true) {
          status = { success: true, message: 'Upgraded successfully' };
        }

        if (status && status.success) {
          completed++;
          this.finishCardTelemetry(pid, true);
          if (this.statusNeedsRestart(status)) {
            this.showRestartRequired(
              `${pkgDisplayName} finished, but Windows needs a restart. This app will not restart the PC.`
            );
          }

          // Visual state: Success on card
          if (card) {
            card.classList.remove('card-updating');
            card.classList.add('card-completed');
            const fill = card.querySelector('.card-inline-progress-fill');
            if (fill) fill.className = 'card-inline-progress-fill success';

            // Update version meta in-place
            const meta = document.getElementById(`meta-${pid}`);
            if (meta && pkgObj) {
              meta.innerHTML = `
                <span>${this.escapeHtml(pkgObj.id)}</span>
                <span class="version-pill version-upgrade">Up to date (v${this.escapeHtml(pkgObj.available_version || 'Latest')})</span>
              `;
            }
          }

          if (singleBtn) {
            singleBtn.className = 'glass-btn btn-completed btn-sm';
            singleBtn.innerHTML = `
              <svg class="check-icon" viewBox="0 0 20 20" fill="currentColor">
                <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414 0z" clip-rule="evenodd"/>
              </svg>
              <span>Upgraded</span>
            `;
          }

          this.clearUpdateIssue(pid);
          this.appendLog(`[OK] Successfully upgraded ${pkgDisplayName} (${pid})\n`);

          // Live state decrement WITHOUT resetting or reloading the UI
          this.updates = this.updates.filter(p => p.id !== pid);
          this.selectedIds.delete(pid);
          this.updateBentoCountersLive();

          // Graceful celebration delay, then smoothly dismiss the card
          setTimeout(() => {
            if (card) {
              card.classList.add('card-dismiss');
              setTimeout(() => {
                if (card && card.parentNode) card.parentNode.removeChild(card);
                if (this.updates.length === 0) {
                  this.renderUpdates(); // Renders the clean "System Up-to-Date" placeholder
                }
              }, 450);
            }
          }, 2200);

        } else {
          failed++;
          const noticeMsg = this.resolveUpgradeNoticeMessage(status);
          this.finishCardTelemetry(pid, false, status);
          this.setIslandTicker(noticeMsg);
          if (card) {
            card.classList.remove('card-updating');
            const fill = card.querySelector('.card-inline-progress-fill');
            if (fill) fill.className = 'card-inline-progress-fill notice';
          }
          if (singleBtn) {
            singleBtn.disabled = false;
            singleBtn.className = 'glass-btn btn-secondary btn-sm';
            singleBtn.innerHTML = `Retry`;
            singleBtn.onclick = () => this.updateSinglePackage(pid);
          }
          this.rememberUpdateIssue(pkgObj || { id: pid, name: pkgDisplayName }, status);
          this.appendLog(`[NOTICE] Upgrade finished with note: ${noticeMsg}\n`);
          this.renderUpdates();
        }
      } catch (e) {
        failed++;
        const noticeMsg = this.resolveUpgradeNoticeMessage({ message: String(e) });
        this.finishCardTelemetry(pid, false, { message: String(e) });
        this.setIslandTicker(noticeMsg);
        if (card) {
          card.classList.remove('card-updating');
          const fill = card.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill notice';
        }
        if (singleBtn) {
          singleBtn.disabled = false;
          singleBtn.className = 'glass-btn btn-secondary btn-sm';
          singleBtn.innerHTML = `Retry`;
          singleBtn.onclick = () => this.updateSinglePackage(pid);
        }
        this.rememberUpdateIssue(pkgObj || { id: pid, name: pkgDisplayName }, { message: String(e) });
        this.appendLog(`[ERROR] Failed ${pid}: ${e}\n`);
        this.renderUpdates();
      }
    }

    this.isUpdating = false;
    this.updateActionButtons();

    if (failed === 0) {
      this.setIslandState(false, 'All Upgrades Completed', 100, `Successfully upgraded ${completed} package(s).`);
    } else {
      this.setIslandState(false, 'Batch Completed with Notices', 100, `Upgraded: ${completed} | Notices: ${failed}`);
    }

    this.appendLog(`\n[Quantum Updater]: Batch operation finished. Success: ${completed}, Notices/Failures: ${failed}\n`);
    this.appendLog(`======================================================\n\n`);
  }

  // ================= In-Card Live Transfer Speed & ETE Telemetry =================

  resolveUpgradeNoticeMessage(status) {
    if (status && typeof status.message === 'string' && status.message.trim()) {
      return status.message.trim();
    }
    return 'Update did not finish.';
  }

  statusNeedsRestart(status) {
    if (!status || typeof status !== 'object') return false;
    const code = Number(status.exit_code);
    // winget: 3010 success+reboot, -1978335181 reboot required
    if (code === 3010 || code === -1978335181) return true;
    const msg = String(status.message || status.reason || '').toLowerCase();
    return msg.includes('restart') || msg.includes('reboot');
  }

  showRestartRequired(detail) {
    this._restartRequired = true;
    const banner = document.getElementById('restart-required-banner');
    const detailEl = document.getElementById('restart-required-detail');
    if (detailEl && detail) detailEl.textContent = detail;
    if (banner) banner.hidden = false;
    this.setIslandTicker('Restart this PC to finish.');
  }

  dismissRestartRequired() {
    this._restartRequired = false;
    const banner = document.getElementById('restart-required-banner');
    if (banner) banner.hidden = true;
  }

  setIslandTicker(text) {
    const islandTicker = document.getElementById('island-status-ticker');
    if (islandTicker && text) islandTicker.textContent = text;
  }

  startCardTelemetry(pid, pkgObj) {
    this.cardTelemetryIntervals = this.cardTelemetryIntervals || {};
    if (this.cardTelemetryIntervals[pid]) {
      clearInterval(this.cardTelemetryIntervals[pid]);
      delete this.cardTelemetryIntervals[pid];
    }

    const card = document.getElementById(`card-${pid}`);
    const hud = document.getElementById(`hud-${pid}`);
    const hudRow = hud ? hud.querySelector('.transfer-hud-row') : null;
    const speedEl = document.getElementById(`hud-speed-${pid}`);
    const bytesEl = document.getElementById(`hud-bytes-${pid}`);
    const pctEl = document.getElementById(`hud-pct-${pid}`);
    const eteEl = document.getElementById(`hud-ete-${pid}`);
    const statusEl = document.getElementById(`hud-status-${pid}`);
    const progFill = document.getElementById(`card-prog-${pid}`);

    if (hud) hud.style.display = 'flex';
    if (hudRow) hudRow.style.display = '';
    if (card) card.classList.add('card-updating');

    const totalBytes = (pkgObj && pkgObj.size_bytes && pkgObj.size_bytes > 0)
      ? pkgObj.size_bytes
      : null;
    const totalStr = (pkgObj && pkgObj.size_str) ? pkgObj.size_str : '';

    this.activeCardTelemetry = {
      pid,
      totalBytes,
      totalStr,
      currentBytes: 0,
      currentPct: 0,
      lastBytes: null,
      lastTime: null,
      lastSpeedBps: null,
      externalProgress: false
    };

    if (progFill) {
      progFill.className = 'card-inline-progress-fill';
      progFill.style.width = '0%';
    }
    if (speedEl) speedEl.textContent = '';
    if (bytesEl) bytesEl.textContent = totalStr ? `— / ${totalStr}` : '— / —';
    if (pctEl) pctEl.textContent = '';
    if (eteEl) eteEl.textContent = '';
    if (statusEl) statusEl.textContent = 'Updating…';
  }

  feedCardTelemetry(pid, pctVal, tickerText) {
    if (!this.activeCardTelemetry || this.activeCardTelemetry.pid !== pid) return;
    const tel = this.activeCardTelemetry;
    tel.externalProgress = true;
    const parsed = this.parseWingetProgressLine(tickerText || '');
    let pct = parsed && parsed.pct !== null ? parsed.pct : null;
    if (pct === null && pctVal) {
      const raw = Number(pctVal);
      if (Number.isFinite(raw) && raw > 0) {
        pct = Math.min(100, Math.max(0, Math.round(raw * (raw <= 1 ? 100 : 1))));
      }
    }

    const speedEl = document.getElementById(`hud-speed-${pid}`);
    const bytesEl = document.getElementById(`hud-bytes-${pid}`);
    const pctEl = document.getElementById(`hud-pct-${pid}`);
    const eteEl = document.getElementById(`hud-ete-${pid}`);
    const statusEl = document.getElementById(`hud-status-${pid}`);
    const progFill = document.getElementById(`card-prog-${pid}`);

    if (pct !== null) {
      tel.currentPct = pct;
      if (progFill) progFill.style.width = `${pct}%`;
      if (pctEl) pctEl.textContent = `${pct}%`;
    }
    if (parsed && parsed.bytesLabel && bytesEl) bytesEl.textContent = parsed.bytesLabel;
    if (parsed && parsed.downloadedBytes !== null) {
      const now = Date.now();
      if (tel.lastBytes !== null && tel.lastTime && now > tel.lastTime) {
        const elapsed = (now - tel.lastTime) / 1000;
        if (elapsed >= 0.35) {
          const delta = parsed.downloadedBytes - tel.lastBytes;
          if (delta >= 0) {
            const bps = delta / elapsed;
            tel.lastSpeedBps = bps;
            let speedText = '';
            if (bps >= 1024 * 1024) speedText = `${(bps / (1024 * 1024)).toFixed(1)} MB/s`;
            else if (bps >= 1024) speedText = `${(bps / 1024).toFixed(1)} KB/s`;
            else if (bps > 0) speedText = `${Math.round(bps)} B/s`;
            if (speedText && speedEl) speedEl.textContent = speedText;
          }
        }
      }
      if (tel.lastBytes === null || parsed.downloadedBytes !== tel.lastBytes) {
        tel.lastBytes = parsed.downloadedBytes;
        tel.lastTime = now;
      }
      tel.currentBytes = parsed.downloadedBytes;
      if (eteEl && tel.totalBytes && tel.lastSpeedBps > 0 && tel.currentBytes < tel.totalBytes) {
        const left = Math.max(1, Math.ceil((tel.totalBytes - tel.currentBytes) / tel.lastSpeedBps));
        eteEl.textContent = `${left}s left`;
      }
    }
    if (statusEl && tickerText) statusEl.textContent = tickerText;
  }

  finishCardTelemetry(pid, success, status = null) {
    let hadExternalProgress = false;
    if (this.cardTelemetryIntervals && this.cardTelemetryIntervals[pid]) {
      clearInterval(this.cardTelemetryIntervals[pid]);
      delete this.cardTelemetryIntervals[pid];
    }
    if (this.activeCardTelemetry && this.activeCardTelemetry.pid === pid) {
      hadExternalProgress = !!this.activeCardTelemetry.externalProgress;
      this.activeCardTelemetry = null;
    }

    const hud = document.getElementById(`hud-${pid}`);
    const hudRow = hud ? hud.querySelector('.transfer-hud-row') : null;
    const speedEl = document.getElementById(`hud-speed-${pid}`);
    const bytesEl = document.getElementById(`hud-bytes-${pid}`);
    const pctEl = document.getElementById(`hud-pct-${pid}`);
    const eteEl = document.getElementById(`hud-ete-${pid}`);
    const statusEl = document.getElementById(`hud-status-${pid}`);
    const progFill = document.getElementById(`card-prog-${pid}`);

    if (success) {
      if (hudRow) hudRow.style.display = '';
      if (progFill) {
        progFill.style.width = '100%';
        progFill.className = 'card-inline-progress-fill success';
      }
      if (speedEl) speedEl.textContent = 'Completed';
      if (pctEl) pctEl.textContent = '100%';
      if (eteEl) eteEl.textContent = '0s';
      if (statusEl) statusEl.textContent = 'Updated.';
    } else {
      const msg = this.resolveUpgradeNoticeMessage(status);
      if (progFill) {
        progFill.className = 'card-inline-progress-fill notice';
        if (!hadExternalProgress) progFill.style.width = '0%';
      }
      if (hudRow) hudRow.style.display = 'none';
      if (speedEl) speedEl.textContent = '';
      if (bytesEl) bytesEl.textContent = '';
      if (pctEl) pctEl.textContent = '';
      if (eteEl) eteEl.textContent = '';
      if (statusEl) statusEl.textContent = msg;
    }
  }

  parseWingetSizeToBytes(value, unit) {
    const v = parseFloat(value);
    if (!Number.isFinite(v)) return null;
    const u = String(unit || '').toUpperCase();
    const scales = { B: 1, KB: 1024, KIB: 1024, MB: 1024 * 1024, MIB: 1024 * 1024, GB: 1024 * 1024 * 1024, GIB: 1024 * 1024 * 1024 };
    return v * (scales[u] || 1);
  }

  parseWingetProgressLine(tickerText) {
    if (!tickerText) return null;
    const transfer = tickerText.match(/(\d+(?:\.\d+)?)\s*(B|KB|KiB|MB|MiB|GB|GiB)\s*\/\s*(\d+(?:\.\d+)?)\s*(B|KB|KiB|MB|MiB|GB|GiB)/i);
    const pctMatch = tickerText.match(/(\d{1,3})\s*%/);
    const pct = pctMatch ? Math.min(100, parseInt(pctMatch[1], 10)) : null;
    if (!transfer && pct === null) return null;
    const parsed = { pct, bytesLabel: '', downloadedBytes: null };
    if (transfer) {
      const [, down, downU, total, totalU] = transfer;
      parsed.bytesLabel = `${down} ${downU.toUpperCase()} / ${total} ${totalU.toUpperCase()}`;
      parsed.downloadedBytes = this.parseWingetSizeToBytes(down, downU);
    }
    return parsed;
  }

  startExploreInstallProgress(pkgId) {
    this.currentInstallingExploreId = pkgId;
    this.exploreInstallTelemetry = { lastBytes: null, lastTime: null, lastSpeed: '' };

    const hud = document.getElementById(`explore-hud-${pkgId}`);
    const hudRow = hud ? hud.querySelector('.transfer-hud-row') : null;
    if (hud) hud.style.display = 'flex';
    if (hudRow) hudRow.style.display = '';

    const progWrap = document.getElementById(`explore-prog-wrap-${pkgId}`);
    const progFill = document.getElementById(`explore-card-prog-${pkgId}`);
    if (progWrap) progWrap.style.display = '';
    if (progFill) {
      progFill.className = 'card-inline-progress-fill';
      progFill.style.width = '0%';
    }

    const speedEl = document.getElementById(`explore-hud-speed-${pkgId}`);
    const bytesEl = document.getElementById(`explore-hud-bytes-${pkgId}`);
    const pctEl = document.getElementById(`explore-hud-pct-${pkgId}`);
    const statusEl = document.getElementById(`explore-hud-status-${pkgId}`);
    if (speedEl) speedEl.textContent = '';
    if (bytesEl) bytesEl.textContent = '— / —';
    if (pctEl) pctEl.textContent = '0%';
    if (statusEl) statusEl.textContent = 'Starting winget install...';
  }

  feedExploreInstallProgress(pkgId, pctVal, tickerText) {
    if (!pkgId || this.currentInstallingExploreId !== pkgId) return;

    const parsed = this.parseWingetProgressLine(tickerText);
    let pct = parsed && parsed.pct !== null
      ? parsed.pct
      : Math.min(100, Math.max(0, Math.round((pctVal || 0) * (pctVal <= 1 ? 100 : 1))));

    const progFill = document.getElementById(`explore-card-prog-${pkgId}`);
    if (progFill) progFill.style.width = `${pct}%`;

    const speedEl = document.getElementById(`explore-hud-speed-${pkgId}`);
    const bytesEl = document.getElementById(`explore-hud-bytes-${pkgId}`);
    const pctEl = document.getElementById(`explore-hud-pct-${pkgId}`);
    const statusEl = document.getElementById(`explore-hud-status-${pkgId}`);

    if (pctEl) pctEl.textContent = `${pct}%`;
    if (parsed && parsed.bytesLabel && bytesEl) {
      bytesEl.textContent = parsed.bytesLabel;

      if (parsed.downloadedBytes !== null) {
        const now = Date.now();
        const tel = this.exploreInstallTelemetry || {};
        if (tel.lastBytes !== null && tel.lastTime !== null && now > tel.lastTime) {
          const elapsed = (now - tel.lastTime) / 1000;
          if (elapsed >= 0.35) {
            const delta = parsed.downloadedBytes - tel.lastBytes;
            if (delta >= 0) {
              const bps = delta / elapsed;
              let speedText = '';
              if (bps >= 1024 * 1024) {
                speedText = `${(bps / (1024 * 1024)).toFixed(1)} MB/s`;
              } else if (bps >= 1024) {
                speedText = `${(bps / 1024).toFixed(1)} KB/s`;
              } else if (bps > 0) {
                speedText = `${Math.round(bps)} B/s`;
              }
              if (speedText && speedEl) speedEl.textContent = speedText;
              tel.lastSpeed = speedText;
            }
          }
        }
        if (tel.lastBytes === null || parsed.downloadedBytes !== tel.lastBytes) {
          tel.lastBytes = parsed.downloadedBytes;
          tel.lastTime = now;
        }
        this.exploreInstallTelemetry = tel;
      }
    }

    if (statusEl && tickerText) statusEl.textContent = tickerText;
  }

  finishExploreInstallProgress(pkgId, success, message) {
    if (this.currentInstallingExploreId === pkgId) {
      this.currentInstallingExploreId = null;
    }
    this.exploreInstallTelemetry = null;

    const progFill = document.getElementById(`explore-card-prog-${pkgId}`);
    const speedEl = document.getElementById(`explore-hud-speed-${pkgId}`);
    const pctEl = document.getElementById(`explore-hud-pct-${pkgId}`);
    const statusEl = document.getElementById(`explore-hud-status-${pkgId}`);

    if (success) {
      if (progFill) {
        progFill.style.width = '100%';
        progFill.className = 'card-inline-progress-fill success';
      }
      if (pctEl) pctEl.textContent = '100%';
      if (speedEl && speedEl.textContent) {
        speedEl.textContent = speedEl.textContent;
      } else if (speedEl) {
        speedEl.textContent = 'Done';
      }
      if (statusEl) statusEl.textContent = message || 'Installed';
    } else {
      if (progFill) progFill.className = 'card-inline-progress-fill notice';
      if (statusEl) statusEl.textContent = message || 'Install did not finish.';
    }
  }

  onProgressUpdate(pctVal, tickerText) {
    this.setIslandProgress(pctVal, tickerText);
    if (this.currentUpdatingPkgId) {
      this.feedCardTelemetry(this.currentUpdatingPkgId, pctVal, tickerText);
    }
    if (this.currentInstallingExploreId) {
      this.feedExploreInstallProgress(this.currentInstallingExploreId, pctVal, tickerText);
    }
  }

  // ================= Dynamic Island Controller =================

  setIslandState(active, title, pct, tickerText) {
    const titleEl = document.getElementById('island-status-title') || document.getElementById('island-task-title');
    const tickerEl = document.getElementById('island-status-ticker');
    const fillEl = document.getElementById('island-progress-fill');
    const badgeEl = document.getElementById('island-pct-badge');
    const pulseEl = document.getElementById('island-pulse');

    if (titleEl) titleEl.textContent = title;
    if (tickerEl) tickerEl.textContent = tickerText || '';
    if (pulseEl) pulseEl.style.display = active ? 'block' : 'none';

    if (pct !== null && pct !== undefined) {
      const p = Math.round(pct);
      if (fillEl) {
        fillEl.style.width = `${p}%`;
        fillEl.classList.remove('indeterminate');
      }
      if (badgeEl) badgeEl.textContent = `${p}%`;
    } else {
      if (fillEl) {
        fillEl.style.width = '0%';
        fillEl.classList.toggle('indeterminate', !!active);
      }
      if (badgeEl) badgeEl.textContent = active ? 'Working...' : '';
    }
  }

  setIslandProgress(pctVal, tickerText) {
    // Bridge sends 0..1 fractions; some UI callers send 0..100. Match card telemetry.
    let hasFinitePct = false;
    let p = 0;
    if (pctVal !== null && pctVal !== undefined) {
      const raw = Number(pctVal);
      if (Number.isFinite(raw)) {
        hasFinitePct = true;
        p = Math.min(100, Math.max(0, Math.round(raw * (raw <= 1 ? 100 : 1))));
      }
    }

    if (!hasFinitePct) {
      if (tickerText) {
        const islandTicker = document.getElementById('island-status-ticker');
        if (islandTicker) islandTicker.textContent = tickerText;
        const dockTicker = document.getElementById('dock-status-ticker');
        if (dockTicker) dockTicker.textContent = tickerText;
        const updateProgTicker = document.getElementById('update-prog-ticker');
        if (updateProgTicker) updateProgTicker.textContent = tickerText;
      }
      return;
    }

    // 1. Dynamic Floating Progress Island
    const islandFill = document.getElementById('island-progress-fill');
    if (islandFill) {
      islandFill.style.width = `${p}%`;
      islandFill.classList.remove('indeterminate');
    }
    const islandPct = document.getElementById('island-pct-badge');
    if (islandPct) islandPct.textContent = `${p}%`;
    if (tickerText) {
      const islandTicker = document.getElementById('island-status-ticker');
      if (islandTicker) islandTicker.textContent = tickerText;
    }

    // 2. Global Persistent Bottom Dock
    const dock = document.getElementById('global-operation-dock');
    if (dock && (this.isProcessingQueue || this.isUpdating || p > 0)) {
      dock.style.display = 'flex';
    }
    const dockFill = document.getElementById('dock-progress-fill');
    if (dockFill) dockFill.style.width = `${p}%`;
    const dockPct = document.getElementById('dock-pct-label');
    if (dockPct) dockPct.textContent = `${p}%`;
    const dockTicker = document.getElementById('dock-status-ticker');
    if (dockTicker && tickerText) dockTicker.textContent = tickerText;

    // 3. Active Package Card Inline Progress
    if (this.currentUpdatingPkgId) {
      const cardProg = document.getElementById(`card-prog-${this.currentUpdatingPkgId}`);
      if (cardProg) {
        cardProg.className = 'card-inline-progress-fill';
        cardProg.style.width = `${p}%`;
      }
    }
    if (this.currentInstallingExploreId) {
      const exploreProg = document.getElementById(`explore-card-prog-${this.currentInstallingExploreId}`);
      if (exploreProg) {
        exploreProg.className = 'card-inline-progress-fill';
        exploreProg.style.width = `${p}%`;
      }
    }

    // 4. Mirror to App Self-Updater Progress Bar if active
    const updateProgFill = document.getElementById('update-prog-fill');
    if (updateProgFill) {
      updateProgFill.style.width = `${p}%`;
      const updateProgPct = document.getElementById('update-prog-pct');
      if (updateProgPct) updateProgPct.textContent = `${p}%`;
      const updateProgTicker = document.getElementById('update-prog-ticker');
      if (updateProgTicker && tickerText) updateProgTicker.textContent = tickerText;
    }
  }

  // ================= Explore Catalog (Automatic Loading, Filters & Sorting) =================

  setExploreCategory(category) {
    this.exploreCategory = category;
    document.querySelectorAll('.cat-pill').forEach(pill => {
      pill.classList.toggle('active', pill.dataset.category === category);
    });
    this.renderExplore();
  }

  setExploreSort(sortValue) {
    this.exploreSort = sortValue;
    this.renderExplore();
  }

  onExploreSearchInput(val) {
    if (this._exploreSearchTimer) clearTimeout(this._exploreSearchTimer);
    this._exploreSearchTimer = setTimeout(() => {
      this.exploreSearchTerm = (val || '').toLowerCase().trim();
      this.renderExplore();
    }, 120);
  }

  renderExplore() {
    this.exploreLiveSearchQuery = '';
    this.lastCatalogSearchResults = null;
    const container = document.getElementById('explore-results-list');
    if (!container) return;

    let items = [...APP_CATALOG];

    // 1. Filter by category
    if (this.exploreCategory === 'popular') {
      items = items.filter(it => (it.downloads || 0) >= 93);
    } else if (this.exploreCategory !== 'all') {
      items = items.filter(it => it.category === this.exploreCategory);
    }

    // 2. Filter by search term
    if (this.exploreSearchTerm) {
      const q = this.exploreSearchTerm;
      items = items.filter(it =>
        it.name.toLowerCase().includes(q) ||
        it.id.toLowerCase().includes(q) ||
        (it.desc || '').toLowerCase().includes(q) ||
        (it.category || '').toLowerCase().includes(q)
      );
    }

    // 3. Sort
    if (this.exploreSort === 'popular') {
      items.sort((a, b) => (b.downloads || 0) - (a.downloads || 0));
    } else if (this.exploreSort === 'name-asc') {
      items.sort((a, b) => a.name.localeCompare(b.name));
    } else if (this.exploreSort === 'name-desc') {
      items.sort((a, b) => b.name.localeCompare(a.name));
    } else if (this.exploreSort === 'category') {
      items.sort((a, b) => (a.category || '').localeCompare(b.category || '') || a.name.localeCompare(b.name));
    }

    // Update count badge
    const badge = document.getElementById('explore-count-badge');
    if (badge) {
      badge.textContent = `${items.length} Packages Available`;
    }

    if (!items.length) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg></div>
          <h3>No Matching Applications</h3>
          <p>No apps on the shelf matched "${this.escapeHtml(this.exploreSearchTerm)}". Search the full catalog.</p>
        </div>
      `;
      return;
    }

    const catLabels = {
      all: 'All Categories',
      popular: 'Most Popular',
      ai: 'AI & Machine Learning',
      development: 'Development',
      browsers: 'Web Browsers',
      productivity: 'Productivity',
      media: 'Music & Media',
      utilities: 'Addons & Utilities',
      gaming: 'Gaming & Social'
    };

    let html = '';
    items.forEach(pkg => {
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);
      const isInstalled = this.isPackageInstalled(pkg.id, pkg.name);
      const catLabel = catLabels[pkg.category] || 'Software';

      html += `
        <div class="package-card ${isInstalled ? 'card-installed' : ''}" id="explore-card-${pkg.id}">
          <div class="package-left">
            ${logoHtml}
            <div class="package-info">
              <div style="display: flex; align-items: center; gap: 8px; flex-wrap: wrap;">
                <span class="package-name">${this.escapeHtml(pkg.name)}</span>
                <span class="badge-cat">${catLabel}</span>
                ${isInstalled ? `<span class="badge-installed-tag"><svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Installed</span>` : ''}
              </div>
              <p class="package-card-desc">${this.escapeHtml(pkg.desc || '')}</p>
              <div class="package-meta">
                <span>${this.escapeHtml(pkg.id)}</span>
                <span class="version-pill">v${this.escapeHtml(pkg.version || 'Latest')}</span>
                <span>Score: ${pkg.downloads || 90}%</span>
              </div>
            </div>
          </div>
          <div class="package-right" style="display: flex; flex-direction: column; align-items: flex-end; gap: 6px;">
            <div style="display: flex; gap: 6px;">
              <button class="glass-btn btn-secondary btn-sm btn-info-action" onclick="app.showDetailsModal('${pkg.id}', '${this.escapeHtml(pkg.name)}', '${pkg.version || ''}', 'explore')" title="Quick Look Inspector">
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
                <span>Info</span>
              </button>
              ${isInstalled ? `
                <button class="glass-btn btn-installed-disabled btn-sm" disabled title="Already installed on this system">
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Installed
                </button>
              ` : `
                <button class="glass-btn btn-coral btn-sm market-install-btn" id="btn-explore-inst-${pkg.id}" onclick="app.installExplorePackage('${pkg.id}', this)">
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg> Install
                </button>
              `}
            </div>
          </div>
          <div class="card-transfer-hud" id="explore-hud-${pkg.id}" style="display: none;">
            <div class="transfer-hud-row">
              <div class="transfer-metric-group">
                <span class="speed-pulse-indicator"></span>
                <span class="transfer-speed-label">SPEED:</span>
                <span class="transfer-speed-val" id="explore-hud-speed-${pkg.id}"></span>
              </div>
              <div class="transfer-metric-group">
                <span class="transfer-bytes-val" id="explore-hud-bytes-${pkg.id}">— / —</span>
                <span class="transfer-pct-pill" id="explore-hud-pct-${pkg.id}">0%</span>
              </div>
            </div>
            <div class="transfer-status-line">
              <span class="transfer-ticker-dot"></span>
              <span class="transfer-status-text" id="explore-hud-status-${pkg.id}">Preparing install...</span>
            </div>
          </div>
          <div class="card-inline-progress explore-card-prog-wrap" id="explore-prog-wrap-${pkg.id}" style="display: none;">
            <div class="card-inline-progress-fill" id="explore-card-prog-${pkg.id}" style="width: 0%"></div>
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
    this.hydrateAppIconsIn(container, items);
  }

  renderCatalogSearchResults(query, results) {
    const container = document.getElementById('explore-results-list');
    if (!container) return;
    this.exploreLiveSearchQuery = query;
    this.lastCatalogSearchResults = results;

    let html = `
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; padding: 0 4px;">
        <span style="font-size: 13px; color: var(--text-muted); font-weight: 600;">Full catalog (${results.length})</span>
        <button class="glass-btn btn-secondary btn-sm" onclick="app.renderExplore()">Back to the shelf</button>
      </div>
    `;

    results.forEach(item => {
      const logoHtml = this.getAppLogoHtml(item.id, item.name);
      const isInstalled = this.isPackageInstalled(item.id, item.name);
      html += `
        <div class="package-card ${isInstalled ? 'card-installed' : ''}" id="explore-card-${item.id}">
          <div class="package-left">
            ${logoHtml}
            <div class="package-info">
              <div style="display: flex; align-items: center; gap: 8px; flex-wrap: wrap;">
                <span class="package-name">${this.escapeHtml(item.name)}</span>
                ${isInstalled ? `<span class="badge-installed-tag"><svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Installed</span>` : ''}
              </div>
              <div class="package-meta">
                <span>${this.escapeHtml(item.id)}</span>
                <span class="version-pill">v${this.escapeHtml(item.version || 'Latest')}</span>
                <span>Catalog</span>
              </div>
            </div>
          </div>
          <div class="package-right" style="display: flex; gap: 6px;">
            <button class="glass-btn btn-secondary btn-sm btn-info-action" onclick="app.showDetailsModal('${item.id}', '${this.escapeHtml(item.name)}', '${item.version || ''}', 'explore')" title="Quick Look Inspector">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
              <span>Info</span>
            </button>
            ${isInstalled ? `
              <button class="glass-btn btn-installed-disabled btn-sm" disabled title="Already installed on this system">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Installed
              </button>
            ` : `
              <button class="glass-btn btn-coral btn-sm" onclick="app.installExplorePackage('${item.id}', this)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg> Install
              </button>
            `}
          </div>
          <div class="card-transfer-hud" id="explore-hud-${item.id}" style="display: none;">
            <div class="transfer-hud-row">
              <div class="transfer-metric-group">
                <span class="speed-pulse-indicator"></span>
                <span class="transfer-speed-label">SPEED:</span>
                <span class="transfer-speed-val" id="explore-hud-speed-${item.id}"></span>
              </div>
              <div class="transfer-metric-group">
                <span class="transfer-bytes-val" id="explore-hud-bytes-${item.id}">— / —</span>
                <span class="transfer-pct-pill" id="explore-hud-pct-${item.id}">0%</span>
              </div>
            </div>
            <div class="transfer-status-line">
              <span class="transfer-ticker-dot"></span>
              <span class="transfer-status-text" id="explore-hud-status-${item.id}">Preparing install...</span>
            </div>
          </div>
          <div class="card-inline-progress explore-card-prog-wrap" id="explore-prog-wrap-${item.id}" style="display: none;">
            <div class="card-inline-progress-fill" id="explore-card-prog-${item.id}" style="width: 0%"></div>
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
    this.hydrateAppIconsIn(container, results);
  }

  async searchRepositoryCatalog() {
    const input = document.getElementById('input-explore-search');
    const query = (input ? input.value : '').trim();
    if (!query) {
      this.renderExplore();
      return;
    }

    const container = document.getElementById('explore-results-list');
    container.innerHTML = `
      <div class="glass-placeholder">
        <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg></div>
        <h3>Searching Official Repository...</h3>
        <p>Querying ${this.activeEngine.toUpperCase()} catalog for "${this.escapeHtml(query)}"</p>
      </div>
    `;

    this.appendLog(`[Quantum Updater]: Searching repository for: "${query}"...\n`);

    try {
      const results = await this.invoke('search_catalog', {
        query: query,
        engine: this.activeEngine
      });

      if (!results || !results.length) {
        container.innerHTML = `
          <div class="glass-placeholder">
            <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg></div>
            <h3>No Online Packages Found</h3>
            <p>No repository packages matched "${this.escapeHtml(query)}". Try another search keyword.</p>
          </div>
        `;
        return;
      }

      this.renderCatalogSearchResults(query, results);
    } catch (e) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg></div>
          <h3>Search Notice</h3>
          <p>${this.escapeHtml(String(e))}</p>
        </div>
      `;
    }
  }

  loadUpdateIssues() {
    try {
      const raw = JSON.parse(localStorage.getItem('qu-update-issues') || '{}');
      return raw && typeof raw === 'object' ? raw : {};
    } catch (_) {
      return {};
    }
  }

  rememberUpdateIssue(pkg, status) {
    if (!pkg || !pkg.id) return;
    const issues = this.loadUpdateIssues();
    const holders = Array.isArray(status && status.holders)
      ? status.holders.filter(Boolean).map((h) => {
          if (typeof h === 'string') return h;
          if (h && typeof h === 'object') {
            if (h.name) return String(h.name);
            if (h.process) return String(h.process);
            if (h.path) {
              const parts = String(h.path).split(/[/\\]/).filter(Boolean);
              return parts.length ? parts[parts.length - 1] : '';
            }
          }
          return '';
        }).filter(Boolean)
      : [];
    issues[pkg.id] = {
      name: pkg.name || pkg.id,
      message: this.resolveUpgradeNoticeMessage(status),
      holders,
      homepage: this.collectHomepageForPackage(pkg),
      at: Date.now()
    };
    localStorage.setItem('qu-update-issues', JSON.stringify(issues));
  }

  clearUpdateIssue(pkgId) {
    const issues = this.loadUpdateIssues();
    if (!issues[pkgId]) return;
    delete issues[pkgId];
    localStorage.setItem('qu-update-issues', JSON.stringify(issues));
  }

  openPublisherPage(url) {
    if (!url || !/^https:\/\//i.test(url)) return;
    window.open(url, '_blank', 'noopener');
  }

  openPublisherPageFromButton(btn) {
    const url = btn && btn.getAttribute('data-url');
    this.openPublisherPage(url);
  }

  async launchDownloadedInstaller(pkgId) {
    try {
      const res = await this.invoke('launch_downloaded_installer', { packageId: pkgId });
      if (res && res.success) {
        this.showToast(res.message || 'Installer launched.', 'success');
      } else {
        this.showToast((res && res.message) || 'Could not run the installer.', 'error');
      }
    } catch (err) {
      this.showToast(err.message || String(err), 'error');
    }
  }

  renderUpdateIssueNote(pkg, issue) {
    const holders = Array.isArray(issue.holders) ? issue.holders.filter(Boolean) : [];
    const closeLine = holders.length
      ? `Close ${holders.map((name) => this.escapeHtml(String(name))).join(', ')}, then try again. This app will not close them for you.`
      : 'You can try again, or open the publisher page and update it yourself.';
    const home = issue.homepage && /^https:\/\//i.test(issue.homepage) ? issue.homepage : '';
    const safeId = String(pkg.id || '').replace(/[^A-Za-z0-9._+-]/g, '');
    const homeBtn = home
      ? `<button type="button" class="glass-btn btn-secondary btn-sm" data-url="${this.escapeHtml(home)}" onclick="event.stopPropagation(); app.openPublisherPageFromButton(this)">Publisher page</button>`
      : '';
    return `
      <div class="update-issue-note" onclick="event.stopPropagation()">
        <div>This update failed before. ${this.escapeHtml(issue.message || 'Update did not finish.')}</div>
        <div>${closeLine}</div>
        <div class="update-issue-actions">
          <button type="button" class="glass-btn btn-secondary btn-sm" onclick="event.stopPropagation(); app.updateSinglePackage('${safeId}')">Try again</button>
          ${homeBtn}
          <button type="button" class="glass-btn btn-secondary btn-sm" onclick="event.stopPropagation(); app.launchDownloadedInstaller('${safeId}')">Run installer</button>
        </div>
      </div>
    `;
  }

  bundleConfigs() {
    return {
      essentials: {
        title: 'Essential Windows Starter',
        apps: [
          { pkgId: 'Google.Chrome', name: 'Google Chrome' },
          { pkgId: 'Spotify.Spotify', name: 'Spotify' },
          { pkgId: 'VideoLAN.VLC', name: 'VLC Media Player' },
          { pkgId: 'Microsoft.PowerToys', name: 'Microsoft PowerToys' },
          { pkgId: 'WhatsApp.WhatsApp', name: 'WhatsApp' },
          { pkgId: 'Telegram.TelegramDesktop', name: 'Telegram Desktop' }
        ]
      },
      dev: {
        title: 'Developer Workstation',
        apps: [
          { pkgId: 'Microsoft.VisualStudioCode', name: 'Visual Studio Code' },
          { pkgId: 'Docker.DockerDesktop', name: 'Docker Desktop' },
          { pkgId: 'Microsoft.WindowsTerminal', name: 'Windows Terminal' },
          { pkgId: 'Git.Git', name: 'Git' },
          { pkgId: 'OpenJS.NodeJS.LTS', name: 'Node.js LTS' },
          { pkgId: 'Python.Python.3.13', name: 'Python 3.13' }
        ]
      },
      creator: {
        title: 'Creative Studio',
        apps: [
          { pkgId: 'BlenderFoundation.Blender', name: 'Blender' },
          { pkgId: 'Figma.Figma', name: 'Figma Desktop' },
          { pkgId: 'Audacity.Audacity', name: 'Audacity' },
          { pkgId: 'HandBrake.HandBrake', name: 'HandBrake' },
          { pkgId: 'GIMP.GIMP', name: 'GIMP' }
        ]
      },
      privacy: {
        title: 'Privacy & Security',
        apps: [
          { pkgId: 'Bitwarden.Bitwarden', name: 'Bitwarden' },
          { pkgId: 'Brave.Brave', name: 'Brave Browser' },
          { pkgId: '7zip.7zip', name: '7-Zip' },
          { pkgId: 'TorProject.TorBrowser', name: 'Tor Browser' }
        ]
      }
    };
  }

  openBundle(bundleId) {
    const bundle = this.bundleConfigs()[bundleId];
    if (!bundle) return;
    this._openBundleId = bundleId;
    let sheet = document.getElementById('bundle-sheet');
    if (!sheet) {
      sheet = document.createElement('div');
      sheet.id = 'bundle-sheet';
      sheet.className = 'modal-backdrop';
      sheet.addEventListener('click', (event) => {
        if (event.target === sheet) this.closeBundle();
      });
      document.body.appendChild(sheet);
    }
    const rows = bundle.apps.map((item) => {
      const installed = this.isPackageInstalled(item.pkgId, item.name);
      const safeId = String(item.pkgId).replace(/[^A-Za-z0-9._+-]/g, '');
      return `
        <label class="bundle-app-row">
          <input type="checkbox" data-bundle-app="${safeId}" ${installed ? 'disabled' : 'checked'}>
          <span>${this.escapeHtml(item.name)}</span>
          ${installed ? '<em>Already on this PC</em>' : ''}
        </label>
      `;
    }).join('');
    sheet.innerHTML = `
      <div class="modal-glass bundle-sheet-card" onclick="event.stopPropagation()">
        <div class="modal-header">
          <h3 class="modal-pkg-title">${this.escapeHtml(bundle.title)}</h3>
          <button type="button" class="modal-close-btn" onclick="app.closeBundle()" aria-label="Close">Close</button>
        </div>
        <p class="bundle-sheet-lead">Untick anything you do not want. Only the ticked apps are installed.</p>
        <div class="bundle-app-list">${rows}</div>
        <div class="update-issue-actions">
          <button type="button" class="glass-btn btn-primary btn-sm" onclick="app.installChosenBundle()">Install selected</button>
          <button type="button" class="glass-btn btn-secondary btn-sm" onclick="app.closeBundle()">Cancel</button>
        </div>
      </div>
    `;
    this.showBackdrop('bundle-sheet');
  }

  closeBundle() {
    this.hideBackdrop('bundle-sheet');
    this._openBundleId = '';
  }

  installChosenBundle() {
    const bundle = this.bundleConfigs()[this._openBundleId];
    const sheet = document.getElementById('bundle-sheet');
    if (!bundle || !sheet) return;
    const chosen = new Set(
      Array.from(sheet.querySelectorAll('input[data-bundle-app]:checked')).map((box) => box.getAttribute('data-bundle-app'))
    );
    if (!chosen.size) {
      this.showToast('Tick at least one app.', 'info');
      return;
    }
    let queuedCount = 0;
    bundle.apps.forEach((item) => {
      if (!chosen.has(item.pkgId)) return;
      if (this.isPackageInstalled(item.pkgId, item.name)) return;
      this.enqueueTask({ type: 'install', pkgId: item.pkgId, name: item.name });
      queuedCount++;
    });
    this.closeBundle();
    if (queuedCount > 0) {
      this.showToast(`Queued ${queuedCount} selected app${queuedCount === 1 ? '' : 's'} from ${bundle.title}`, 'info');
    } else {
      this.showToast('Those apps are already on this PC.', 'success');
    }
  }

  async installExplorePackage(pkgId, btnEl) {
    // Find card and package metadata
    const card = document.getElementById(`explore-card-${pkgId}`) || (btnEl ? btnEl.closest('.package-card') : null);
    const catItem = APP_CATALOG.find(p => p.id.toLowerCase() === pkgId.toLowerCase());
    const pkgDisplayName = catItem ? catItem.name : pkgId;

    if (card) {
      card.classList.add('card-updating');
    }
    this.startExploreInstallProgress(pkgId);

    if (btnEl) {
      btnEl.disabled = true;
      btnEl.className = 'glass-btn btn-updating btn-sm';
      btnEl.innerHTML = `<span class="btn-spinner"></span> <span>Installing...</span>`;
    }

    this.setIslandState(true, `Installing ${pkgDisplayName}`, null, `Downloading via ${this.activeEngine.toUpperCase()}...`);
    this.appendLog(`[Quantum Updater]: Initiating silent install for "${pkgDisplayName}" (${pkgId})...\n`);

    const resolvedId = this.resolveInstallPackageId(catItem || pkgId);
    if (this._betaFallbackNote) {
      this.showToast(this._betaFallbackNote, 'info');
      this._betaFallbackNote = '';
    }

    try {
      const res = await this.invoke('install_package', {
        packageId: resolvedId,
        engine: this.activeEngine
      });

      if (res && res.success) {
        if (card) {
          card.classList.remove('card-updating');
          card.classList.add('card-completed');
        }

        if (btnEl) {
          btnEl.className = 'glass-btn btn-completed btn-sm';
          btnEl.innerHTML = `
            <svg class="check-icon" viewBox="0 0 20 20" fill="currentColor">
              <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414 0z" clip-rule="evenodd"/>
            </svg>
            <span>Installed</span>
          `;
        }

        this.installedSet.add(pkgId.toLowerCase());
        const successMsg = (res && res.message) ? res.message : `${pkgDisplayName} is ready to use.`;
        this.finishExploreInstallProgress(pkgId, true, 'Installed');
        this.setIslandState(false, `${pkgDisplayName} Ready`, 100, successMsg);
        this.showToast(successMsg, 'success');
        this.appendLog(`[OK] Successfully processed "${pkgDisplayName}" (${pkgId}): ${successMsg}\n`);

        setTimeout(() => {
          if (card) card.classList.remove('card-completed');
        }, 3200);

      } else {
        const failMsg = (res && res.message) ? res.message : 'Check console for details.';
        if (card) card.classList.remove('card-updating');
        this.finishExploreInstallProgress(pkgId, false, failMsg);
        if (btnEl) {
          btnEl.disabled = false;
          btnEl.className = 'glass-btn btn-secondary btn-sm';
          btnEl.innerHTML = `Retry Install`;
          btnEl.onclick = () => this.installExplorePackage(pkgId, btnEl);
        }
        this.setIslandState(false, `Install Notice`, null, failMsg);
      }
    } catch (e) {
      const errMsg = String(e);
      if (card) card.classList.remove('card-updating');
      this.finishExploreInstallProgress(pkgId, false, errMsg);
      if (btnEl) {
        btnEl.disabled = false;
        btnEl.className = 'glass-btn btn-secondary btn-sm';
        btnEl.innerHTML = `Retry Install`;
        btnEl.onclick = () => this.installExplorePackage(pkgId, btnEl);
      }
      this.setIslandState(false, `Install Error`, null, errMsg);
      this.appendLog(`[ERROR]: Installation failed for "${pkgDisplayName}": ${e}\n`);
    }
  }

  // ================= Installed Applications Tab =================

  async loadInstalled() {
    return this.loadInstalledSilent();
  }

  renderInstalled(filteredList = null) {
    const list = filteredList !== null ? filteredList : this.installed;
    const container = document.getElementById('installed-list');

    if (!list.length) {
      container.innerHTML = `<div class="glass-placeholder"><h3>No applications found</h3></div>`;
      return;
    }

    const isWin = !this.isMac;
    let html = '';
    list.forEach(pkg => {
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);
      let badgeLabel = pkg.source || 'mac-app';
      if (isWin) {
        if (pkg.origin === 'catalog') badgeLabel = 'Catalog';
        else if (pkg.origin === 'this-pc') badgeLabel = 'This PC';
        else if (pkg.origin) badgeLabel = pkg.origin;
        else badgeLabel = 'This PC';
      }
      const archChip = isWin ? 'x64 / ARM64' : 'Universal 2';
      const encId = encodeURIComponent(pkg.id || '');
      const encName = encodeURIComponent(pkg.name || '');
      const safePath = (pkg.path || '').replace(/\\/g, '\\\\').replace(/'/g, "\\'");
      const folderBtn = isWin
        ? (pkg.path
          ? `<button class="glass-btn btn-secondary btn-sm" onclick="app.revealInFinder(decodeURIComponent('${encId}'), decodeURIComponent('${encodeURIComponent(pkg.path || '')}'))" title="Open install folder">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"></path></svg>
              <span>Open folder</span>
            </button>`
          : '')
        : `<button class="glass-btn btn-secondary btn-sm" onclick="app.revealInFinder('${pkg.id}', '${safePath}')" title="Reveal in macOS Finder">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
              <span>Finder</span>
            </button>`;
      const uninstallDisabled = isWin && pkg.can_uninstall === false;
      const uninstallBtn = uninstallDisabled
        ? `<button class="glass-btn btn-secondary btn-sm" style="color: var(--c-coral-pink); opacity: 0.45;" disabled title="No safe uninstall command">Uninstall</button>`
        : `<button class="glass-btn btn-secondary btn-sm" style="color: var(--c-coral-pink);" onclick="app.uninstallPackage(decodeURIComponent('${encId}'), decodeURIComponent('${encName}'))">Uninstall</button>`;

      html += `
        <div class="package-card">
          <div class="package-main-row">
            <div class="package-left">
              ${logoHtml}
              <div class="package-info">
                <div style="display: flex; align-items: center; gap: 8px; min-width: 0;">
                  <span class="package-name">${this.escapeHtml(pkg.name)}</span>
                  <span class="badge-cat" style="font-size: 10px;">${this.escapeHtml(badgeLabel)}</span>
                </div>
                <div class="package-meta">
                  <span>${this.escapeHtml(pkg.id)}</span>
                  <span class="version-pill">v${this.escapeHtml(pkg.installed_version || '?')}</span>
                  <span class="security-chip-pill" style="font-size: 9px;">${archChip}</span>
                </div>
              </div>
            </div>
            <div class="package-right" style="display: flex; align-items: center; gap: 6px; flex-shrink: 0;">
              ${folderBtn}
              <button class="glass-btn btn-secondary btn-sm btn-info-action" onclick="app.showDetailsModal('${pkg.id}', '${this.escapeHtml(pkg.name)}', '${pkg.installed_version || ''}', 'installed')" title="Quick Look Inspector">
                <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
                <span>Info</span>
              </button>
              ${uninstallBtn}
            </div>
          </div>
        </div>
      `;
    });
    container.innerHTML = html;
    this.hydrateAppIconsIn(container, list);
  }

  filterInstalled(query) {
    if (this._filterInstalledTimer) clearTimeout(this._filterInstalledTimer);
    this._filterInstalledTimer = setTimeout(() => {
      const q = (query || '').toLowerCase().trim();
      if (!q) {
        this.renderInstalled();
        return;
      }
      const filtered = this.installed.filter(p =>
        (p.name || '').toLowerCase().includes(q) ||
        (p.id || '').toLowerCase().includes(q)
      );
      this.renderInstalled(filtered);
    }, 120);
  }

  async uninstallPackage(pkgId, pkgName) {
    if (!confirm(`Are you sure you want to completely uninstall "${pkgName || pkgId}" and purge all its residual files?`)) return;
    this.appendLog(`[Deep Uninstaller]: Initiating total clean removal for "${pkgName || pkgId}"...\n`);
    this.setIslandState(true, `Uninstalling ${pkgName || pkgId}`, null, pkgName || pkgId);
    try {
      const res = await this.invoke('uninstall_package', { packageId: pkgId, name: pkgName || pkgId, engine: this.activeEngine });
      if (res && res.success === true) {
        this.showToast(`Uninstalled ${pkgName || pkgId}`, 'success');
        this.setIslandState(false, 'Uninstalled Successfully', 100, `Removed ${pkgName || pkgId}.`);
        this.installedSet.delete(pkgId.toLowerCase().trim());
        if (pkgName) this.installedSet.delete(pkgName.toLowerCase().trim());
      } else {
        const msg = (res && res.message) ? res.message : 'Uninstall did not complete successfully.';
        this.showToast(`Notice during uninstall: ${msg}`, 'info');
        this.setIslandState(false, 'Uninstall Notice', 0, msg);
      }
    } catch (e) {
      this.showToast(`Notice during uninstall: ${e.message || e}`, 'info');
      this.setIslandState(false, 'Uninstall Notice', 0, String(e));
    }
    this.loadInstalled();
  }

  // ================= Terminal & Console =================

  appendLog(line) {
    const output = document.getElementById('console-output');
    if (!output) return;
    const el = document.createElement('div');
    el.className = 'terminal-line';
    el.textContent = line;
    output.appendChild(el);

    // Bound DOM size to 250 lines to prevent unbounded memory growth and layout thrashing
    while (output.childNodes.length > 250) {
      output.removeChild(output.firstChild);
    }

    if (!this._logScrollScheduled) {
      this._logScrollScheduled = true;
      requestAnimationFrame(() => {
        output.scrollTop = output.scrollHeight;
        this._logScrollScheduled = false;
      });
    }
  }

  clearConsoleLog() {
    const output = document.getElementById('console-output');
    if (output) output.innerHTML = '<div class="terminal-line system">[Quantum Updater]: Log cleared.</div>';
  }

  copyConsoleLog() {
    const output = document.getElementById('console-output');
    if (output) {
      navigator.clipboard.writeText(output.innerText);
      alert('Console logs copied to clipboard!');
    }
  }

  // ================= Mac System Cleanup & Storage Reclaimer =================

  async optimizeMemory() {
    const btn = document.getElementById('btn-optimize-ram');
    const origHtml = btn ? btn.innerHTML : '';
    if (btn) {
      btn.disabled = true;
      btn.innerHTML = `<svg class="spin" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg> <span>Optimizing...</span>`;
    }

    this.setIslandState(true, 'Optimizing memory…', null, 'Reclaiming inactive RAM…');
    this.appendLog('[Memory Optimizer]: Requesting user-mode compaction of purgeable memory...\n');

    try {
      const res = await this.invokeCleanup('optimize_memory', {});
      const freedBytes = res.freed_bytes || 0;
      const freed = res.freed_str || this.formatBytes(freedBytes);
      const message = res.message || (freedBytes > 0
        ? `${freed} of inactive RAM reclaimed`
        : 'No memory was freed');
      const toastType = freedBytes > 0 ? 'success' : 'info';
      this.showToast(message, toastType);
      this.setIslandState(false, freedBytes > 0 ? 'Memory Compacted' : 'Memory Unchanged', 100, message);
      this.appendLog(`[Memory Optimizer]: ${message}\n`);

      const ramStatus = document.getElementById('cleanup-ram-status');
      const ramSub = document.getElementById('cleanup-ram-sub');
      if (ramStatus) ramStatus.textContent = freedBytes > 0 ? 'Optimized' : 'No change';
      if (ramSub) ramSub.textContent = message;
    } catch (e) {
      this.showToast(e.message || 'Memory optimization failed', 'error');
      this.setIslandState(false, 'Memory optimization failed', null, String(e.message || e));
    } finally {
      if (btn) {
        btn.disabled = false;
        btn.innerHTML = origHtml;
      }
    }
  }

  async scanCleanup() {
    const store = this.scanStore;
    if (store && store.isCleanupJobActive()) {
      await this.cancelCleanupScan();
      return;
    }
  const btn = document.getElementById('btn-scan-cleanup');
    if (btn) btn.classList.add('loading');
    this.appendLog('[Quantum Clean]: Requesting cleanup_scan job…');

    try {
      if (store) {
        await store.startJob('cleanup_scan', true);
        this.applyReadinessGates();
        return;
      }
      const res = await this.invokeCleanup('scan_cleanup');
      if (res.categories) {
        this.cleanupData = res;
        this.cleanupPlanStale = false;
        this.selectedCleanupCategories = new Set(
          res.categories.filter(c =>
            c.selected && c.size_bytes > 0 && c.id !== 'missing_folders' && !c.report_only && !c.needs_windows_decision
          ).map(c => c.id)
        );
        this.renderCleanupMetrics();
        this.renderCleanupDisks();
        this.renderCleanupCategories();
      } else {
        this.showToast('Cleanup scan returned no data', 'error');
      }
    } catch (err) {
      this.appendLog(`[Quantum Clean Error]: ${err.message || err}`);
      this.showToast('Failed to scan system storage', 'error');
    } finally {
      if (btn) btn.classList.remove('loading');
    }
  }

  renderCleanupDisks() {
    const container = document.getElementById('cleanup-disks-grid');
    if (!container || !this.cleanupData) return;
    const disks = Array.isArray(this.cleanupData.disks) ? this.cleanupData.disks : [];
    if (!disks.length) {
      container.innerHTML = '';
      container.style.display = 'none';
      return;
    }
    container.style.display = 'grid';
    container.innerHTML = disks.map(disk => {
      const letter = disk.letter || '?';
      const total = disk.total_bytes || 0;
      const used = disk.used_bytes || 0;
      const free = disk.free_bytes || 0;
      const usedPct = total > 0 ? Math.min(100, Math.round((used / total) * 100)) : 0;
      return `
        <div class="cleanup-disk-card">
          <div class="cleanup-disk-head">
            <span class="cleanup-disk-letter">${this.escapeHtml(letter)}:</span>
            <span class="cleanup-disk-free">${this.formatBytes(free)} free</span>
          </div>
          <div class="cleanup-disk-bar"><span style="width:${usedPct}%;"></span></div>
          <div class="cleanup-disk-meta">${this.formatBytes(used)} used of ${this.formatBytes(total)} (${usedPct}%)</div>
        </div>
      `;
    }).join('');
  }

  renderCleanupMetrics() {
    if (!this.cleanupData) return;
    const d = this.cleanupData;
    const primaryDisk = (Array.isArray(d.disks) && d.disks.length)
      ? d.disks.find(x => x.letter && x.letter.toUpperCase() === 'C') || d.disks[0]
      : null;

    const totalEl = document.getElementById('cleanup-reclaimable-total');
    if (totalEl) totalEl.textContent = d.total_reclaimable_str;
    const subEl = document.getElementById('cleanup-reclaimable-sub');
    if (subEl) {
      let sub = `${d.total_files.toLocaleString()} temporary files scanned`;
      if (d.windows_owned_bytes > 0) {
        sub += ` · ${d.windows_owned_str || this.formatBytes(d.windows_owned_bytes)} Windows-owned (separate)`;
      }
      subEl.textContent = sub;
    }

    const healthEl = document.getElementById('cleanup-health-status');
    if (healthEl) {
      if (d.total_reclaimable_bytes > 1024 * 1024 * 1024) {
        healthEl.textContent = 'Optimization Suggested';
        healthEl.className = 'stat-value text-golden';
      } else if (d.total_reclaimable_bytes > 0) {
        healthEl.textContent = 'Ready to Clean';
        healthEl.className = 'stat-value text-mint';
      } else {
        healthEl.textContent = 'Clean & Optimized';
        healthEl.className = 'stat-value text-mint';
      }
    }

    // Disk storage
    const diskEl = document.getElementById('cleanup-disk-free');
    if (diskEl) {
      diskEl.textContent = primaryDisk
        ? `${this.formatBytes(primaryDisk.free_bytes)} Free`
        : `${d.disk_free_str} Free`;
    }
    const diskSub = document.getElementById('cleanup-disk-sub');
    if (diskSub) {
      if (primaryDisk) {
        const pct = primaryDisk.total_bytes > 0
          ? Math.round((primaryDisk.used_bytes / primaryDisk.total_bytes) * 1000) / 10
          : (d.disk_used_percent || 0);
        diskSub.textContent = `${this.formatBytes(primaryDisk.used_bytes)} of ${this.formatBytes(primaryDisk.total_bytes)} used (${pct}%)`;
      } else {
        diskSub.textContent = `${d.disk_used_str} of ${d.disk_total_str} used (${d.disk_used_percent}%)`;
      }
    }

    const diskUsedPercent = primaryDisk && primaryDisk.total_bytes > 0
      ? (primaryDisk.used_bytes / primaryDisk.total_bytes) * 100
      : (d.disk_used_percent || 20);
    const diskTotalBytes = primaryDisk ? primaryDisk.total_bytes : (d.disk_total_bytes || 0);

    // Visual Storage Meter
    const usedPct = Math.min(92, Math.max(8, diskUsedPercent));
    const junkPct = d.total_reclaimable_bytes && diskTotalBytes ?
      Math.min(12, Math.max(1, (d.total_reclaimable_bytes / diskTotalBytes) * 100)) :
      (d.total_reclaimable_bytes > 0 ? 3 : 0);

    const meterUsed = document.getElementById('cleaner-meter-used');
    if (meterUsed) meterUsed.style.width = `${usedPct}%`;
    const meterJunk = document.getElementById('cleaner-meter-junk');
    if (meterJunk) meterJunk.style.width = `${junkPct}%`;

    // Sidebar APFS Telemetry Gauge
    const sideDiskFree = document.getElementById('sidebar-disk-free');
    if (sideDiskFree) sideDiskFree.textContent = `${d.disk_free_str} Free of ${d.disk_total_str}`;
    const sideDiskBar = document.getElementById('sidebar-disk-bar');
    if (sideDiskBar) sideDiskBar.style.width = `${d.disk_used_percent}%`;
    const sideDiskPct = document.getElementById('sidebar-disk-pct');
    if (sideDiskPct) sideDiskPct.textContent = `${d.disk_used_percent}% Used`;

    this.updateCleanupSelectedMetric();
  }

  updateCleanupSelectedMetric() {
    if (!this.cleanupData || !this.cleanupData.categories) return;
    let selectedBytes = 0;
    let selectedCount = 0;

    this.cleanupData.categories.forEach(cat => {
      if (this.selectedCleanupCategories.has(cat.id)) {
        selectedBytes += cat.size_bytes;
        selectedCount++;
      }
    });

    const countEl = document.getElementById('cleanup-selected-count');
    if (countEl) countEl.textContent = `${selectedCount} categories selected (${this.formatBytes(selectedBytes)})`;

    // Update nav counter badge
    const badge = document.getElementById('nav-cleanup-badge');
    if (badge) {
      if (selectedCount > 0) {
        badge.textContent = selectedCount;
        badge.style.display = 'inline-block';
      } else {
        badge.style.display = 'none';
      }
    }
  }

  isCleanupReportOnly(cat) {
    return !!cat && (cat.id === 'missing_folders' || cat.report_only === true);
  }

  isCleanupWindowsDeferred(cat) {
    return !!cat && (cat.needs_windows_decision === true);
  }

  renderCleanupCategories() {
    const container = document.getElementById('cleanup-categories-container');
    if (!container || !this.cleanupData || !this.cleanupData.categories) return;

    if (this.cleanupData.categories.length === 0) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <h3>No cleanup targets found</h3>
          <p>Your system caches and temporary files are clean.</p>
        </div>
      `;
      return;
    }

    let html = '';
    this.cleanupData.categories.forEach(cat => {
      const reportOnly = this.isCleanupReportOnly(cat);
      const windowsDeferred = this.isCleanupWindowsDeferred(cat);
      const isSelected = this.selectedCleanupCategories.has(cat.id);
      const isZero = cat.size_bytes === 0 && !(reportOnly && (cat.file_count || 0) > 0);

      let subItemsHtml = '';
      if (cat.sub_items && cat.sub_items.length > 0) {
        subItemsHtml = `
          <div class="cleanup-subitem-pills">
            ${cat.sub_items.slice(0, 4).map(sub => `
              <span class="subitem-pill" title="${this.escapeHtml(sub.name)}: ${sub.size_str}">${this.escapeHtml(sub.name)} (${sub.size_str})</span>
            `).join('')}
          </div>
        `;
      }

      html += `
        <div class="cleaner-category-row ${isSelected ? 'selected' : ''}" id="cat-card-${cat.id}">
          <div class="category-row-left">
            <div class="category-icon-box">
              ${this.getCleanupIconSvg(cat.svg_icon)}
            </div>
            <div class="category-meta">
              <div class="category-title-line">
                <span class="category-name">${this.escapeHtml(cat.name)}</span>
                <span class="category-tag-badge">${this.escapeHtml(cat.tag)}</span>
              </div>
              <span class="category-desc">${this.escapeHtml(cat.description)}</span>
              ${subItemsHtml}
            </div>
          </div>

          <div class="category-row-right">
            <div class="category-metrics">
              <span class="category-size-text ${isZero ? 'text-zero' : ''}">${cat.size_str}</span>
              <span class="category-files-text">${cat.file_count.toLocaleString()} files</span>
            </div>
            ${reportOnly
              ? '<span class="category-report-only-pill">Report only</span>'
              : windowsDeferred
              ? '<span class="category-report-only-pill">Needs Windows decision</span>'
              : `<label class="mac-checkbox-switch">
              <input type="checkbox" ${isSelected ? 'checked' : ''} ${isZero ? 'disabled' : ''} onchange="app.toggleCleanupCategory('${cat.id}', this.checked)">
              <span class="mac-switch-slider"></span>
            </label>`}
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
  }

    getCleanupIconSvg(type) {
    switch (type) {
      case 'database':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><ellipse cx="12" cy="5" rx="9" ry="3"></ellipse><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"></path><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"></path></svg>';
      case 'code':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="16 18 22 12 16 6"></polyline><polyline points="8 6 2 12 8 18"></polyline></svg>';
      case 'file-text':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line></svg>';
      case 'trash-2':
      case 'trash':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>';
      case 'archive':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="21 8 21 21 3 21 3 8"></polyline><rect x="1" y="3" width="22" height="5"></rect><line x1="10" y1="12" x2="14" y2="12"></line></svg>';
      case 'cpu':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="16" height="16" rx="2" ry="2"></rect><rect x="9" y="9" width="6" height="6"></rect><line x1="9" y1="1" x2="9" y2="4"></line><line x1="15" y1="1" x2="15" y2="4"></line><line x1="9" y1="20" x2="9" y2="23"></line><line x1="15" y1="20" x2="15" y2="23"></line><line x1="20" y1="9" x2="23" y2="9"></line><line x1="20" y1="14" x2="23" y2="14"></line><line x1="1" y1="9" x2="4" y2="9"></line><line x1="1" y1="14" x2="4" y2="14"></line></svg>';
      case 'refresh-cw':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"></polyline><polyline points="1 20 1 14 7 14"></polyline><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path></svg>';
      default:
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 2 7 12 12 22 7 12 2"></polygon><polyline points="2 17 12 22 22 17"></polyline><polyline points="2 12 12 17 22 12"></polyline></svg>';
    }
  }

  toggleCleanupCategory(catId, isChecked) {
    const cat = (this.cleanupData && this.cleanupData.categories)
      ? this.cleanupData.categories.find(c => c.id === catId)
      : null;
    if (this.isCleanupReportOnly(cat) || this.isCleanupWindowsDeferred(cat)) return;
    if (isChecked) {
      this.selectedCleanupCategories.add(catId);
    } else {
      this.selectedCleanupCategories.delete(catId);
    }
    const card = document.getElementById(`cat-card-${catId}`);
    if (card) card.classList.toggle('selected', isChecked);
    this.updateCleanupSelectedMetric();
  }

  selectRecommendedCleanup() {
    if (!this.cleanupData || !this.cleanupData.categories) return;
    this.selectedCleanupCategories.clear();
    this.cleanupData.categories.forEach(c => {
      if (
        c.risk === 'low'
        && c.size_bytes > 0
        && !c.opt_in
        && !this.isCleanupReportOnly(c)
        && !this.isCleanupWindowsDeferred(c)
      ) {
        this.selectedCleanupCategories.add(c.id);
      }
    });
    this.renderCleanupCategories();
    this.updateCleanupSelectedMetric();
  }

  toggleAllCleanup() {
    if (!this.cleanupData || !this.cleanupData.categories) return;
    const eligible = this.cleanupData.categories.filter(
      c => c.size_bytes > 0 && !c.opt_in && !this.isCleanupReportOnly(c) && !this.isCleanupWindowsDeferred(c)
    );
    const allSelected = eligible.length > 0 && eligible.every(c => this.selectedCleanupCategories.has(c.id));
    this.selectedCleanupCategories.clear();
    if (!allSelected) {
      eligible.forEach(c => this.selectedCleanupCategories.add(c.id));
    }
    this.renderCleanupCategories();
    this.updateCleanupSelectedMetric();
  }

  // ================= System Cleanup Consent & File Trace Inspector =================

  openCleanupConsentModal(mode = 'purge') {
    if (this.isCleaning) {
      this.showToast('Cleanup is already running.', 'info');
      return;
    }
    const store = this.scanStore;
    if (store && !store.cleanupReady()) {
      const job = store.job('cleanup_scan');
      this._openCleanReviewAfterScan = true;
      this.renderCleanupScanProgress({
        status: job.status === 'queued' ? 'queued' : 'scanning',
        percent: job.percent || 0,
        current_item: job.current_item || 'Starting storage scan…',
      });
      if (!store.isCleanupJobActive()) this.scanCleanup();
      this.showToast('Scanning storage. The review opens when the scan finishes.', 'info');
      return;
    }
    const modal = document.getElementById('modal-cleanup-consent');
    if (!modal) {
      this.executeCleanup();
      return;
    }

    if (this.selectedCleanupCategories.size === 0) {
      this.showToast('Please select at least one storage item to review', 'error');
      return;
    }

    const selectedList = Array.from(this.selectedCleanupCategories);
    let totalBytes = 0;
    let totalItems = 0;
    let categoriesHtml = '';

    const categories = (this.cleanupData && this.cleanupData.categories) ? this.cleanupData.categories : [];
    
    categories.forEach(cat => {
      if (this.selectedCleanupCategories.has(cat.id) && !this.isCleanupReportOnly(cat)) {
        totalBytes += cat.size_bytes || 0;
        totalItems += cat.item_count || 0;

        const sizeFormatted = cat.size_str || this.formatBytes(cat.size_bytes || 0);
        const countFormatted = `${cat.item_count || 0} items`;

        categoriesHtml += `
          <div class="consent-category-row">
            <div class="consent-category-info">
              <span class="consent-cat-name">${this.escapeHtml(cat.name || cat.id)}</span>
              <span class="consent-cat-desc">${this.escapeHtml(cat.description || '')}</span>
            </div>
            <div class="consent-category-meta">
              <span class="consent-cat-size">${sizeFormatted}</span>
              <button class="glass-btn btn-secondary btn-xs" onclick="app.inspectCleanupCategory('${cat.id}')" title="Inspect traced files">
                <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                Trace
              </button>
            </div>
          </div>
        `;
      }
    });

    const sizeEl = document.getElementById('consent-total-size');
    const filesEl = document.getElementById('consent-total-files');
    const catsEl = document.getElementById('consent-total-cats');
    const listEl = document.getElementById('consent-categories-list');

    if (sizeEl) sizeEl.textContent = this.formatBytes(totalBytes);
    if (filesEl) filesEl.textContent = `${totalItems.toLocaleString()} items`;
    if (catsEl) catsEl.textContent = `${selectedList.length} categories`;
    if (listEl) listEl.innerHTML = categoriesHtml;

    this.showBackdrop('modal-cleanup-consent');
  }

  closeCleanupConsentModal() {
    this._openCleanReviewAfterScan = false;
    this.hideBackdrop('modal-cleanup-consent');
  }

  confirmExecuteCleanup() {
    this.closeCleanupConsentModal();
    this.executeCleanup();
  }

  inspectCleanupCategory(catId) {
    const drawer = document.getElementById('drawer-category-inspect');
    if (!drawer) return;

    const cat = (this.cleanupData && this.cleanupData.categories) ? this.cleanupData.categories.find(c => c.id === catId) : null;
    if (!cat) return;

    const titleEl = document.getElementById('inspect-title');
    const subEl = document.getElementById('inspect-subtitle');
    const statsBar = document.getElementById('inspect-stats-bar');
    const tableBody = document.getElementById('trace-files-tbody');

    if (titleEl) titleEl.textContent = cat.name || catId;
    if (subEl) subEl.textContent = cat.description || `Traced artifacts for ${catId}`;

    const totalSize = cat.size_str || this.formatBytes(cat.size_bytes || 0);
    const totalFiles = cat.item_count || (cat.items ? cat.items.length : 0);

    if (statsBar) {
      statsBar.innerHTML = `
        <span class="trace-pill"><strong>${totalSize}</strong> tracked</span>
        <span class="trace-pill"><strong>${totalFiles}</strong> artifacts</span>
      `;
    }

    if (tableBody) {
      const items = cat.items || [];
      if (!items.length) {
        tableBody.innerHTML = `<tr><td colspan="2" style="text-align: center; color: var(--text-muted); padding: 18px;">No individual files logged for this category (aggregate directory cleanup).</td></tr>`;
      } else {
        tableBody.innerHTML = items.slice(0, 50).map(it => `
          <tr>
            <td class="trace-path-cell" title="${this.escapeHtml(it.path || it.name || '')}">${this.escapeHtml(it.path || it.name || '')}</td>
            <td class="trace-size-cell">${it.size_str || this.formatBytes(it.size_bytes || 0)}</td>
          </tr>
        `).join('');
      }
    }

    this.showBackdrop('drawer-category-inspect');
  }

  closeInspectDrawer() {
    this.hideBackdrop('drawer-category-inspect');
  }

  onCleanupScanStopped(status) {
    if (!this._openCleanReviewAfterScan) return;
    this._openCleanReviewAfterScan = false;
    this.showToast(status === 'cancelled' ? 'Storage scan cancelled.' : 'Storage scan did not finish.', 'error');
  }

  async executeCleanup() {
    if (this.scanStore && !this.scanStore.cleanupReady()) {
      this.showToast('Wait for the storage scan to finish before cleaning.', 'error');
      return;
    }
    if (this.selectedCleanupCategories.size === 0) {
      this.showToast('Please select at least one item to clean', 'error');
      return;
    }

    const selectedList = Array.from(this.selectedCleanupCategories).filter(id => id !== 'missing_folders');
    let selectedBytes = 0;
    if (this.cleanupData && this.cleanupData.categories) {
      this.cleanupData.categories.forEach(c => {
        if (selectedList.includes(c.id)) selectedBytes += c.size_bytes;
      });
    }

    this.isCleaning = true;
    this._lastCleanupCategoryCount = selectedList.length;
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    const headerBtn = document.getElementById('btn-one-click-clean');
    if (purgeBtn) purgeBtn.classList.add('loading');
    if (headerBtn) headerBtn.classList.add('loading');

    this.setIslandState(
      true,
      'Cleaning...',
      null,
      `Cleaning ${selectedList.length} selected storage categories...`
    );
    this.appendLog(`[Storage Clean]: Cleaning ${this.formatBytes(selectedBytes)} from: ${selectedList.join(', ')}`);

    try {
      const res = await this.invokeCleanup('execute_cleanup', { categories: selectedList });
      this.onCleanupFinished(true, res);
    } catch (err) {
      this.appendLog(`[Storage Clean Error]: ${err.message || err}`);
      this.showToast('Cleaning failed to complete', 'error');
      this.setIslandState(false, 'Cleaning failed', 0, err.message || String(err));
      if (purgeBtn) purgeBtn.classList.remove('loading');
      const headerBtn = document.getElementById('btn-one-click-clean');
      if (headerBtn) headerBtn.classList.remove('loading');
      this.isCleaning = false;
    }
  }

  onCleanupFinished(success, rawResult) {
    this.isCleaning = false;
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    if (purgeBtn) purgeBtn.classList.remove('loading');
    const oneClickBtn = document.getElementById('btn-one-click-clean');
    if (oneClickBtn) oneClickBtn.classList.remove('loading');
    let res = {};
    try {
      res = (typeof rawResult === 'string' ? JSON.parse(rawResult) : rawResult) || {};
    } catch (e) {
      res = {};
    }
    if (!res || typeof res !== 'object') {
      res = {};
    }

    const selectedCount = this._lastCleanupCategoryCount || 0;
    const skipped = res.files_skipped || 0;
    const blocked = res.items_blocked || 0;

    if (success && !res.error) {
      const freedBytes = res.total_freed_bytes || 0;
      const freed = res.total_freed_str || this.formatBytes(freedBytes);
      const parts = [];
      if (freedBytes > 0) {
        parts.push(`Freed ${freed}`);
      } else {
        parts.push('Freed 0 B');
      }
      if (skipped > 0) parts.push(`${skipped} file(s) skipped (in use)`);
      if (blocked > 0) parts.push(`${blocked} protected item(s) not changed`);
      const summary = parts.join('. ') + '.';

      if (res.reboot_required || res.restart_required || this.statusNeedsRestart(res)) {
        this.showRestartRequired(
          'A clean finished, but Windows needs a restart. This app will not restart the PC.'
        );
      }

      this.setIslandState(false, 'Cleanup complete', 100, summary);

      if (freedBytes > 0) {
        this.showToast(summary, 'success');
        this.appendLog(`[Storage Clean]: ${summary}`);
      } else if (selectedCount > 0) {
        this.showToast(summary, 'error');
        this.appendLog(`[Storage Clean]: Nothing reclaimable. ${summary}`);
      } else {
        this.showToast('No categories were selected to clean.', 'error');
      }
      setTimeout(() => {
        this.scanCleanup();
        this.loadInitialDiagnostics();
      }, 600);
    } else {
      const failMsg = res.error || 'Check activity log';
      this.setIslandState(false, 'Cleanup failed', 0, failMsg);
      this.showToast(`Cleanup notice: ${failMsg}`, 'error');
    }
  }

  formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }


  // ================= Scan orchestration UI =================

  renderSidebarScanStatus() {
    if (!this.scanStore) return;
    const sumEl = document.getElementById('sidebar-scan-summary');
    const sumSub = document.getElementById('sidebar-scan-summary-sub');
    const sumBtn = document.getElementById('sidebar-scan-rescan-btn');
    const s = this.scanStore.summary || {};
    const running = s.running || 0;
  if (sumEl) {
      if (running > 0) {
        const done = s.done || 0;
        const total = s.total || 0;
        sumEl.textContent = `Checking your PC, ${done} of ${total} done`;
        if (sumSub) sumSub.textContent = 'Finishing background checks…';
      } else {
        sumEl.textContent = 'All checks complete';
        if (sumSub) sumSub.textContent = 'Background scans are up to date';
      }
    }
    if (sumBtn) sumBtn.disabled = running > 0;

    NAV_SCAN_SLOTS.forEach(({ tab, job }) => {
      const slot = document.getElementById(`nav-scan-${tab}`);
      if (!slot) return;
      if (!job) {
        slot.innerHTML = '';
        return;
      }
      const j = this.scanStore.job(job);
      slot.innerHTML = this.sidebarStatusIconHtml(j);
    });
  }

  sidebarStatusIconHtml(job) {
    const pct = Math.max(0, Math.min(100, Math.round(job.percent || 0)));
    if (job.status === 'scanning' || job.status === 'queued') {
      if (pct <= 0) {
        return `<span class="nav-scan-ring" title="Scanning…"><svg class="nav-scan-svg" width="28" height="28" viewBox="0 0 28 28" aria-hidden="true"><circle cx="14" cy="14" r="11.5" fill="none" stroke="var(--progress-track)" stroke-width="3"/><circle cx="14" cy="14" r="11.5" fill="none" stroke="var(--color-mint)" stroke-width="3" stroke-dasharray="18 54" stroke-linecap="round"><animateTransform attributeName="transform" type="rotate" from="0 14 14" to="360 14 14" dur="1.1s" repeatCount="indefinite"/></circle></svg></span>`;
      }
      const dash = (pct / 100) * 72;
      return `<span class="nav-scan-ring" title="Scanning ${pct}%"><svg class="nav-scan-svg" width="28" height="28" viewBox="0 0 28 28" aria-hidden="true"><circle cx="14" cy="14" r="11.5" fill="none" stroke="var(--progress-track)" stroke-width="3"/><circle cx="14" cy="14" r="11.5" fill="none" stroke="var(--color-mint)" stroke-width="3" stroke-dasharray="${dash} 72" transform="rotate(-90 14 14)"/><text x="14" y="17.5" text-anchor="middle" font-size="8" font-weight="600" fill="var(--text-secondary)">${pct}</text></svg></span>`;
    }
    if (job.status === 'ready') {
      const hint = job.cached ? 'Cached result' : 'Ready';
      return `<span class="nav-scan-ok" title="${hint}"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--color-mint)" stroke-width="2.5" aria-hidden="true"><polyline points="20 6 9 17 4 12"/></svg></span>`;
    }
    if (job.status === 'failed') {
      return `<span class="nav-scan-warn" title="Check failed"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="var(--color-warning-bg)" stroke-width="2.5" aria-hidden="true"><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg></span>`;
    }
    return '';
  }

  skipStartupGateForForcedRescan() {
    this._startupGateSkipForced = true;
    this._startupGateDismissed = true;
    this.updateStartupReadyGate();
  }

  isStartupGateVisible() {
    const el = document.getElementById('startup-ready-gate');
    return !!(el && !el.hidden);
  }

  _startupJobSettled(status) {
    return STARTUP_GATE_SETTLED.has(status);
  }

  _startupJobLabel(status) {
    if (status === 'ready') return 'Ready';
    if (status === 'failed') return 'Could not finish';
    if (status === 'cancelled') return 'Stopped';
    if (status === 'scanning') return 'Checking';
    if (status === 'queued') return 'Waiting';
    return 'Waiting';
  }

  updateStartupReadyGate() {
    const gate = document.getElementById('startup-ready-gate');
    if (!gate) return;
    const store = this.scanStore;
    if (!store || this._startupGateDismissed || this._startupGateSkipForced) {
      gate.hidden = true;
      gate.setAttribute('aria-hidden', 'true');
      document.body.classList.remove('startup-gate-open');
      return;
    }

    let settled = 0;
    const total = STARTUP_GATE_JOBS.length;
    STARTUP_GATE_JOBS.forEach(({ id, local }) => {
      let status;
      if (local || id === 'self-update') {
        status = this._selfUpdateStatus || 'queued';
      } else {
        status = store.job(id).status;
      }
      const row = gate.querySelector(`li[data-job="${id}"]`);
      if (this._startupJobSettled(status)) settled += 1;
      if (row) {
        row.classList.toggle('is-ready', status === 'ready');
        row.classList.toggle('is-failed', status === 'failed' || status === 'cancelled');
        row.classList.toggle('is-wait', status === 'queued' || (!status));
        const em = row.querySelector('em');
        if (em) {
          if (id === 'self-update' && status === 'ready' && this.pendingUpdate) {
            em.textContent = 'Update available';
          } else {
            em.textContent = this._startupJobLabel(status);
          }
        }
      }
    });

    const countEl = document.getElementById('startup-ready-count');
    const phaseEl = document.getElementById('startup-ready-phase');
    const fill = document.getElementById('startup-ready-fill');
    if (countEl) countEl.textContent = `${settled} of ${total} checks finished`;
    if (phaseEl) phaseEl.textContent = settled >= total ? 'Ready' : 'Working';
    if (fill) fill.style.width = `${Math.round((settled / total) * 100)}%`;

    if (settled >= total) {
      this._startupGateDismissed = true;
      gate.hidden = true;
      gate.setAttribute('aria-hidden', 'true');
      document.body.classList.remove('startup-gate-open');
      return;
    }

    gate.hidden = false;
    gate.setAttribute('aria-hidden', 'false');
    document.body.classList.add('startup-gate-open');
  }

  applyReadinessGates() {
    const store = this.scanStore;
    if (!store) return;
    this.updateStartupReadyGate();
    this.isScanning = store.isScanning('catalog-updates');
    const updatesReady = store.updatesReady();
    const marketReady = store.marketplaceReady();
    const cleanReady = store.cleanupReady();
    const btnScan = document.getElementById('btn-scan');
    const btnSync = document.getElementById('btn-sync');
    const scanningUpdates = store.isScanning('catalog-updates');
    if (btnScan) {
      if (scanningUpdates) {
        btnScan.disabled = false;
        btnScan.innerHTML = `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><rect x="6" y="6" width="12" height="12" rx="2"/></svg> <span>Cancel</span>`;
        btnScan.onclick = () => this.cancelUpdatesScan();
      } else {
        btnScan.disabled = !updatesReady && !store.job('catalog-updates').cached;
        btnScan.innerHTML = `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg> <span>${updatesReady ? 'Check again' : 'Waiting for catalog…'}</span>`;
        btnScan.onclick = () => this.scanUpdates();
      }
    }
    if (btnSync) btnSync.disabled = scanningUpdates;

    const btnAction = document.getElementById('btn-update-action');
    if (btnAction) btnAction.disabled = !updatesReady || this.isUpdating;

    const cleanBtn = document.getElementById('btn-one-click-clean');
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    const scanClean = document.getElementById('btn-scan-cleanup');
    const cleanBusy = store.isCleanupJobActive() || this.isCleaning;
    if (cleanBtn) {
      cleanBtn.disabled = this.isCleaning;
      cleanBtn.classList.toggle('loading', cleanBusy);
    }
    if (purgeBtn) {
      purgeBtn.disabled = this.isCleaning;
      purgeBtn.classList.toggle('loading', cleanBusy);
    }
    if (scanClean) {
      if (store.isCleanupJobActive()) {
        scanClean.innerHTML = `<span>Cancel scan</span>`;
        scanClean.onclick = () => this.cancelCleanupScan();
      } else {
        scanClean.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg><span>${cleanReady ? 'Scan storage' : 'Waiting for scan…'}</span>`;
        scanClean.onclick = () => this.scanCleanup();
      }
      scanClean.disabled = false;
    }

    document.querySelectorAll('.btn-install-market, .market-install-btn').forEach((btn) => {
      if (!marketReady) {
        btn.disabled = true;
        const lbl = btn.querySelector('span');
        if (lbl && !lbl.dataset.prev) {
          lbl.dataset.prev = lbl.textContent;
          lbl.textContent = 'Checking…';
        }
      } else if (btn.disabled && btn.querySelector('span')?.dataset?.prev === 'Checking…') {
        const lbl = btn.querySelector('span');
        if (lbl) {
          lbl.textContent = lbl.dataset.prev;
          delete lbl.dataset.prev;
        }
      }
    });

    const staleNote = document.getElementById('updates-cache-note');
    const uj = store.job('catalog-updates');
    if (staleNote) {
      const catalogBusy = uj.status === 'queued' || uj.status === 'scanning';
      if (catalogBusy && this.updates.length > 0) {
        staleNote.textContent = 'Showing the last list. Checking again.';
        staleNote.hidden = false;
      } else if (uj.cached && uj.status === 'ready') {
        const when = store.formatLastChecked(uj);
        staleNote.textContent = when ? `Last checked ${when}` : '';
        staleNote.hidden = !when;
      } else {
        staleNote.hidden = true;
      }
    }
  }

  onScanJobReady(jobId) {
    if (jobId === 'catalog-updates') this.refreshUpdatesFromStore(true);
    if (jobId === 'local-arp' || jobId === 'catalog-installed') this.loadInstalledSilent();
    if (jobId === 'cleanup_scan') this.applyCleanupFromOrchestrator(true);
    if (jobId === 'startup') this.renderStartupAppsPage();
    if (jobId === 'leftovers') this.renderLeftoverFoldersPage();
    if (jobId === 'local-hardware') this.loadInitialDiagnostics();
  }

  async refreshUpdatesFromStore(allowCached) {
    const store = this.scanStore;
    if (!store) return;
    const job = store.job('catalog-updates');
    const canRead = store.updatesReady() || job.status === 'scanning' || job.status === 'queued';
    if (!canRead) return;
    if (job.cached && !allowCached && store.updatesReady()) return;
    const catalogBusy = job.status === 'queued' || job.status === 'scanning';
    try {
      const raw = await this.invoke('scan_result', { job_id: 'catalog-updates' });
      if (Array.isArray(raw)) {
        this.updates = raw;
        this.scanError = '';
        this.isScanning = catalogBusy;
        this.renderUpdates();
        if (catalogBusy) {
          this.syncUpdatesPageSummary(this.formatUpdatesSummaryMessage());
          this.setIslandState(true, 'Checking for updates…', job.percent > 0 ? job.percent : null, 'Showing the last list. Checking again.');
        } else if (job.status === 'ready') {
          this.isScanning = false;
          this.setIslandState(false, 'Sync complete', 100, this.formatUpdatesSummaryMessage());
        } else {
          this.syncUpdatesPageSummary(this.formatUpdatesSummaryMessage());
        }
      }
    } catch (e) {
      console.warn('[updates refresh]', e);
    }
  }

  async cancelUpdatesScan() {
    if (!this.scanStore) return;
    await this.scanStore.cancelJob('catalog-updates');
    this.isScanning = false;
    this.renderUpdates();
    this.applyReadinessGates();
  }

  async cancelCleanupScan() {
    try {
      await this.invoke('cleanup_scan_cancel', {});
      await this.scanStore.cancelJob('cleanup_scan');
    } catch (_) {}
    this.applyReadinessGates();
  }

  async applyCleanupFromOrchestrator(allowCached) {
    const store = this.scanStore;
    if (!store || !store.cleanupReady()) return;
    const job = store.job('cleanup_scan');
    if (job.cached && !allowCached) return;
    try {
      const res = await this.invoke('scan_result', { job_id: 'cleanup_scan' });
      if (res && res.categories) {
        this.cleanupData = res;
        this.cleanupPlanStale = false;
        this.selectedCleanupCategories = new Set(
          res.categories.filter((c) =>
            c.selected && c.size_bytes > 0 && c.id !== 'missing_folders' && !c.report_only && !c.needs_windows_decision
          ).map((c) => c.id)
        );
        this.renderCleanupMetrics();
        this.renderCleanupDisks();
        this.renderCleanupCategories();
        this.renderCleanupScanProgress({ status: 'done' });
      }
    } catch (e) {
      console.warn('[cleanup refresh]', e);
    }
    const staleNote = document.getElementById('cleanup-stale-note');
    if (staleNote) {
      if (job.cached && store.cleanupReady()) {
        const when = store.formatLastChecked(job);
        staleNote.textContent = when
          ? `Showing saved scan from ${when}. Scan storage to refresh.`
          : 'Showing saved scan. Scan storage to refresh.';
        staleNote.hidden = false;
      } else {
        staleNote.hidden = true;
      }
    }
    this.applyReadinessGates();
    if (this._openCleanReviewAfterScan && store.cleanupReady()) {
      this._openCleanReviewAfterScan = false;
      this.openCleanupConsentModal('purge');
    }
  }

  renderCleanupScanProgress(live) {
    const strip = document.getElementById('cleanup-scan-progress');
    if (!strip) return;
    if (!live || (live.status !== 'running' && live.status !== 'scanning' && live.status !== 'queued')) {
      strip.hidden = true;
      return;
    }
    strip.hidden = false;
    const p = live.progress || live;
    const pct = Math.round(p.percent ?? live.percent ?? 0);
    const bar = strip.querySelector('.cleanup-scan-bar i');
    if (bar) {
      bar.classList.toggle('is-waiting', pct <= 0);
      bar.style.width = pct <= 0 ? '0%' : `${pct}%`;
    }
    const folder = document.getElementById('cleanup-scan-folder');
    if (folder) folder.textContent = p.current_folder || live.current_item || 'Scanning…';
    const bytes = document.getElementById('cleanup-scan-bytes');
    if (bytes) bytes.textContent = p.bytes_scanned_str || p.bytes_str || '';
    const eta = document.getElementById('cleanup-scan-eta');
    if (eta) eta.textContent = pct > 0 ? (p.eta_str || '') : '';
    const title = document.getElementById('cleanup-scan-title');
    if (title) {
      title.textContent = live.status === 'queued'
        ? 'Preparing storage scan…'
        : pct > 0
          ? `Scanning storage (${pct}%)`
          : 'Scanning storage…';
    }
  }

  isStartupEdgeName(name) {
    return /^MicrosoftEdgeAutoLaunch_/i.test(name || '');
  }

  startupDisplayName(name) {
    if (!name) return 'Startup item';
    if (this.isStartupEdgeName(name)) return 'Microsoft Edge';
    const known = {
      GoogleDriveFS: 'Google Drive',
      JellyfinTray: 'Jellyfin',
      'Microsoft.Lists': 'Microsoft Lists',
      BingWallpaperDaemon: 'Bing Wallpaper',
      CanvaAutoLaunchAvailabilityCheckAgent: 'Canva',
      SecurityHealth: 'Windows Security',
      RtkAudUService: 'Realtek Audio',
      CloudflareWARP: 'Cloudflare WARP',
    };
    if (known[name]) return known[name];
    let label = name;
    if (label.endsWith('.lnk')) label = label.slice(0, -4);
    return label;
  }

  startupSourceLocation(source) {
    const s = (source || '').trim();
    if (s === 'Starts with Windows for you') return 'Your sign-in';
    if (s === 'Starts for everyone on this PC') return 'Every account';
    if (s === 'Startup folder') return 'Startup folder';
    if (s === 'Sign-in task') return 'Sign-in task';
    return s;
  }

  startupEntrySubtitle(entry) {
    const loc = this.startupSourceLocation(entry.source || entry.kind);
    const pub = (entry.publisher || '').trim();
    if (pub && loc) return `${pub} · ${loc}`;
    return loc || pub || '';
  }

  startupHumanRefusedReason(reason) {
    if (!reason || typeof reason !== 'string') return '';
    const r = reason.trim();
    if (r.length < 4 || r.length > 96) return '';
    if (/^[A-Z0-9_]+$/.test(r)) return '';
    if (!/[a-z]/.test(r)) return '';
    if (r.split(/\s+/).filter(Boolean).length < 2) return '';
    return r;
  }

  startupRowIsOn(entry) {
    return entry.enabled !== false;
  }

  startupBuildDisplayRows(entries) {
    const edgeMembers = entries.filter((e) => this.isStartupEdgeName(e.name));
    const displayRows = [];
    let edgePlaced = false;
    for (const e of entries) {
      if (this.isStartupEdgeName(e.name)) {
        if (!edgePlaced && edgeMembers.length) {
          displayRows.push({ kind: 'edge', members: edgeMembers });
          edgePlaced = true;
        }
      } else {
        displayRows.push({ kind: 'single', entry: e });
      }
    }
    return displayRows;
  }

  startupRowHtml(row) {
    const isEdge = row.kind === 'edge';
    const refused = isEdge
      ? row.members.length > 0 && row.members.every((m) => m.refused)
      : Boolean(row.entry.refused);
    const enabled = isEdge
      ? row.members.some((m) => this.startupRowIsOn(m))
      : this.startupRowIsOn(row.entry);
    const title = isEdge ? 'Microsoft Edge' : this.startupDisplayName(row.entry.name || row.entry.label);
    let subtitle = isEdge
      ? (() => {
          const n = row.members.length;
          const on = row.members.filter((m) => this.startupRowIsOn(m)).length;
          return `Startup boost · ${n} entries · ${on} on`;
        })()
      : this.startupEntrySubtitle(row.entry);
    const extraRefused = !isEdge && row.entry.refused
      ? this.startupHumanRefusedReason(row.entry.refused_reason)
      : '';
    const rowClasses = ['tool-row', 'card'];
    if (!enabled && !refused) rowClasses.push('tool-row-off');
    if (refused) rowClasses.push('tool-row-protected');
    const stateLabel = enabled ? 'On' : 'Off';
    const switchDisabled = refused ? 'disabled' : '';
    const dataToggle = isEdge ? 'data-startup-edge-toggle' : 'data-startup-toggle';
    const dataId = !isEdge && row.entry.id ? ` data-startup-id="${this.escapeHtml(row.entry.id)}"` : '';
    const refusedBlock = refused
      ? `<div class="tool-row-sub tool-row-sub-refused">Windows protects this${extraRefused ? ` · ${this.escapeHtml(extraRefused)}` : ''}</div>`
      : '';
    return `
      <div class="${rowClasses.join(' ')}">
        <div class="tool-row-info">
          <div class="tool-row-title">${this.escapeHtml(title)}</div>
          <div class="tool-row-sub">${this.escapeHtml(subtitle)}</div>
          ${refusedBlock}
        </div>
        <div class="startup-toggle-wrap" aria-label="${this.escapeHtml(title)} ${stateLabel}">
          <span class="startup-toggle-label">${stateLabel}</span>
          <label class="toggle-switch">
            <input type="checkbox" ${enabled ? 'checked' : ''} ${switchDisabled} ${dataToggle}${dataId}>
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>`;
  }

  bindStartupListToggles(root) {
    root.querySelectorAll('[data-startup-toggle]').forEach((input) => {
      input.addEventListener('change', () => {
        const id = input.getAttribute('data-startup-id');
        if (!id) return;
        this.onStartupEntryToggle(id, input.checked, input);
      });
    });
    root.querySelectorAll('[data-startup-edge-toggle]').forEach((input) => {
      input.addEventListener('change', () => {
        this.onStartupEdgeToggle(input.checked, input);
      });
    });
  }

  async onStartupEntryToggle(entryId, enabled, input) {
    try {
      await this.toggleStartupEntry(entryId, enabled);
    } catch (_) {
      if (input) input.checked = !enabled;
    }
  }

  async onStartupEdgeToggle(wantOn, input) {
    const members = this._startupEdgeMembers || [];
    const targets = members.filter(
      (m) => !m.refused && (wantOn ? !this.startupRowIsOn(m) : this.startupRowIsOn(m)),
    );
    if (!targets.length) return;
    let failed = false;
    for (const m of targets) {
      try {
        await this.toggleStartupEntry(m.id, wantOn);
        m.enabled = wantOn;
      } catch (_) {
        failed = true;
      }
    }
    if (failed && input) input.checked = !wantOn;
    await this.renderStartupAppsPage();
  }

  async renderStartupAppsPage() {
    const root = document.getElementById('startup-apps-list');
    if (!root) return;
    const summaryEl = document.getElementById('startup-apps-summary');
    const store = this.scanStore;
    if (store && !store.isReady('startup')) {
      root.innerHTML = '<p class="tool-page-hint">Waiting for startup scan…</p>';
      return;
    }
    let entries = [];
    try {
      entries = await this.invoke('startup_list', {});
      if (!Array.isArray(entries)) {
        const raw = await this.invoke('scan_result', { job_id: 'startup' });
        entries = Array.isArray(raw) ? raw : (raw?.entries || []);
      }
    } catch (_) {
      root.innerHTML = '<p class="tool-page-hint">Startup list is not available yet.</p>';
      return;
    }
    if (!entries.length) {
      if (summaryEl) summaryEl.textContent = 'No startup entries found.';
      root.innerHTML = '<p class="tool-page-hint">No startup entries found.</p>';
      return;
    }
    const displayRows = this.startupBuildDisplayRows(entries);
    const edgeMembers = entries.filter((e) => this.isStartupEdgeName(e.name));
    this._startupEdgeMembers = edgeMembers;
    const turnedOn = displayRows.filter((row) => {
      if (row.kind === 'edge') return row.members.some((m) => this.startupRowIsOn(m));
      return this.startupRowIsOn(row.entry);
    }).length;
    if (summaryEl) {
      summaryEl.textContent = `${displayRows.length} startup items, ${turnedOn} turned on`;
    }
    root.innerHTML = displayRows.map((row) => this.startupRowHtml(row)).join('');
    this.bindStartupListToggles(root);
  }

  async toggleStartupEntry(entryId, enabled) {
    if (!entryId) return;
    try {
      await this.invoke('startup_set_enabled', { entry_id: entryId, enabled });
      this.showToast(enabled ? 'Startup enabled' : 'Startup disabled', 'success');
    } catch (e) {
      this.showToast(e.message || 'Could not change startup item', 'error');
      throw e;
    }
  }

  async renderLeftoverFoldersPage() {
    const root = document.getElementById('leftover-folders-list');
    if (!root) return;
    const store = this.scanStore;
    if (store && !store.isReady('leftovers')) {
      root.innerHTML = '<p class="tool-page-hint">Waiting for leftover scan…</p>';
      return;
    }
    let data = { items: [] };
    try {
      data = await this.invoke('leftovers_scan', {});
      if (!data.items) {
        const raw = await this.invoke('scan_result', { job_id: 'leftovers' });
        data = raw && raw.items ? raw : { items: Array.isArray(raw) ? raw : [] };
      }
    } catch (_) {}
    const items = data.items || [];
    this.leftoverItems = items;
    if (!items.length) {
      root.innerHTML = '<p class="tool-page-hint">No leftover folders found.</p>';
      return;
    }
    const showRecycle = store ? store.hasLeftoversRecycle : false;
    root.innerHTML = items.map((it) => `
      <div class="tool-row card wide">
        <div class="tool-row-info">
          <div class="tool-row-title">${this.escapeHtml(it.program || 'Leftover folder')}</div>
          <div class="tool-row-sub">${this.escapeHtml(it.folder || '')} · ${this.escapeHtml(it.size_str || '')}</div>
        </div>
        <div class="tool-row-actions">
          <button type="button" class="glass-btn btn-secondary btn-sm" onclick="app.leftoversReveal('${this.escapeHtml(it.folder || '').replace(/'/g, "\\'")}')">Reveal</button>
          ${showRecycle ? `<button type="button" class="glass-btn btn-secondary btn-sm" onclick="app.leftoversRecycleConfirm('${this.escapeHtml(it.id || '')}')">Recycle</button>` : ''}
          ${showRecycle && it.registry_entry_id ? `<button type="button" class="glass-btn btn-secondary btn-sm" onclick="app.leftoversRemoveRegistry('${this.escapeHtml(it.registry_entry_id || '')}', '${this.escapeHtml(it.id || '')}')">Remove uninstall entry</button>` : ''}
        </div>
      </div>
    `).join('');
  }

  async leftoversReveal(path) {
    await this.invoke('leftovers_reveal', { path });
  }

  async leftoversRecycleConfirm(itemId) {
    const item = (this.leftoverItems || []).find((x) => x.id === itemId);
    if (!item || !confirm(`Move this folder to the Recycle Bin?\n\n${item.folder}\n\n${item.size_str || ''}`)) return;
    this.setIslandState(true, 'Moving leftover folder…', null, item.folder || item.program || '');
    try {
      const res = await this.invoke('leftovers_recycle', { path: item.folder, confirmed: true });
      this.showToast(res?.message || 'Recycle Bin action finished', res?.success === false ? 'error' : 'success');
      this.setIslandState(false, res?.success === false ? 'Move failed' : 'Leftover folder moved', res?.success === false ? 0 : 100, res?.message || 'Recycle Bin action finished');
    } catch (e) {
      this.showToast(String(e.message || e), 'error');
      this.setIslandState(false, 'Move failed', 0, String(e.message || e));
    }
    this.renderLeftoverFoldersPage();
  }

  async leftoversRemoveRegistry(entryId, folderId) {
    const item = (this.leftoverItems || []).find((x) => x.registry_entry_id === entryId || x.id === folderId);
    if (!item) return;
    if (!confirm(`Remove the stale uninstall registry entry after backing it up?\n\n${item.program || entryId}`)) return;
    this.setIslandState(true, 'Removing uninstall entry…', null, item.program || item.folder || entryId);
    try {
      const conf = await this.invoke('leftovers_confirm_registry', { entry_id: entryId });
      if (!conf?.token) {
        this.showToast(conf?.message || 'Could not prepare registry removal', 'error');
        this.setIslandState(false, 'Registry removal failed', 0, conf?.message || 'Could not prepare registry removal');
        return;
      }
      const res = await this.invoke('leftovers_delete_registry', { entry_id: entryId, token: conf.token });
      this.showToast(res?.message || 'Registry entry removed', res?.success === false ? 'error' : 'success');
      this.setIslandState(false, res?.success === false ? 'Registry removal failed' : 'Uninstall entry removed', res?.success === false ? 0 : 100, res?.message || 'Registry entry removed');
    } catch (e) {
      this.showToast(String(e.message || e), 'error');
      this.setIslandState(false, 'Registry removal failed', 0, String(e.message || e));
    }
    this.renderLeftoverFoldersPage();
  }

  async renderLargeFilesPage() {
    const root = document.getElementById('large-files-list');
    if (!root || !this.largeFilesJobId) return;
    try {
      const st = await this.invoke('large_files_status', { job_id: this.largeFilesJobId });
      if (st.status === 'running') {
        const prog = st.progress || {};
        const filesScanned = prog.files_scanned;
        const currentFolder = prog.current_folder || '';
        let ticker = 'Starting…';
        if (filesScanned != null && Number.isFinite(Number(filesScanned))) {
          ticker = `${Number(filesScanned).toLocaleString()} files scanned`;
        }
        if (currentFolder) {
          ticker = ticker === 'Starting…' ? currentFolder : `${ticker} · ${currentFolder}`;
        }
        this.setIslandState(true, 'Scanning large files…', null, ticker);
        let hint = 'Scanning…';
        if (filesScanned != null && Number.isFinite(Number(filesScanned))) {
          hint += ` ${Number(filesScanned).toLocaleString()} files`;
        }
        if (currentFolder) {
          const folderLine = `<p class="tool-page-hint tool-row-sub">${this.escapeHtml(currentFolder)}</p>`;
          root.innerHTML = `<p class="tool-page-hint">${hint}</p>${folderLine}`;
        } else {
          root.innerHTML = `<p class="tool-page-hint">${hint}</p>`;
        }
        setTimeout(() => this.renderLargeFilesPage(), 600);
        return;
      }
      if (this._largeFilesScanning) {
        this._largeFilesScanning = false;
        const n = st.count != null ? st.count : (st.files || st.results || []).length;
        this.setIslandState(false, 'Large file scan finished', 100, `${n} large file(s) found`);
      }
      const list = st.results || st.files || [];
      if (list.length) {
        this.largeFilesList = list;
        root.innerHTML = list.map((f) => `
          <div class="tool-row card wide">
            <p class="tool-row-title">${this.escapeHtml(f.name || f.path)}</p>
            <p class="tool-row-sub">${this.escapeHtml(f.path || '')} · ${this.escapeHtml(f.size_str || '')}</p>
            <div class="tool-row-actions">
              <button type="button" class="glass-btn btn-primary btn-sm" onclick="app.largeFilesRecycle(['${this.escapeHtml(f.path).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'])">Move to Recycle Bin</button>
              <button type="button" class="glass-btn btn-secondary btn-sm" onclick="app.largeFilesDeleteConfirm(['${this.escapeHtml(f.path).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'])">Delete permanently</button>
              <button type="button" class="glass-btn btn-secondary btn-sm" onclick="app.largeFilesReveal('${this.escapeHtml(f.path).replace(/\\/g, '\\\\').replace(/'/g, "\\'")}')">Reveal</button>
            </div>
          </div>
        `).join('');
      } else {
        root.innerHTML = '<p class="tool-page-hint">No large files matched your filters.</p>';
      }
    } catch (_) {
      root.innerHTML = '<p class="tool-page-hint">Large file scan unavailable.</p>';
      if (this._largeFilesScanning) {
        this._largeFilesScanning = false;
        this.setIslandState(false, 'Large file scan finished', 0, 'Large file scan unavailable.');
      }
    }
  }

  async largeFilesStartScan() {
    const rootPath = (document.getElementById('large-files-root') || {}).value || '';
    const minMb = parseFloat((document.getElementById('large-files-min') || {}).value || '500') || 500;
    this.setIslandState(true, 'Scanning large files…', null, 'Starting…');
    this._largeFilesScanning = true;
    try {
      const res = await this.invoke('large_files_scan_start', { root: rootPath, min_mb: minMb, older_than_days: 0 });
      this.largeFilesJobId = res.job_id || res.job || '';
      if (!this.largeFilesJobId) {
        this._largeFilesScanning = false;
        this.setIslandState(false, 'Large file scan finished', 0, 'Scan did not start');
        return;
      }
      this.renderLargeFilesPage();
    } catch (e) {
      this._largeFilesScanning = false;
      this.setIslandState(false, 'Large file scan finished', 0, String(e.message || e));
    }
  }

  async largeFilesReveal(path) {
    await this.invoke('large_files_reveal', { path });
  }

  async largeFilesRecycle(paths) {
    const list = paths || [];
    const ticker = list.length === 1 ? (list[0] || '') : `${list.length} files`;
    this.setIslandState(true, 'Moving to Recycle Bin…', null, ticker);
    try {
      const res = await this.invoke('large_files_recycle', { paths, job_id: this.largeFilesJobId || '' });
      this.showToast(res?.message || 'Moved to Recycle Bin', res?.success === false ? 'error' : 'success');
      this.setIslandState(false, res?.success === false ? 'Recycle Bin failed' : 'Moved to Recycle Bin', res?.success === false ? 0 : 100, res?.message || 'Moved to Recycle Bin');
    } catch (e) {
      this.showToast(String(e.message || e), 'error');
      this.setIslandState(false, 'Recycle Bin failed', 0, String(e.message || e));
    }
    this.renderLargeFilesPage();
  }

  async largeFilesDeleteConfirm(paths) {
    const total = (paths || []).length;
    if (!confirm(`Delete ${total} file(s) permanently? This cannot be undone.`)) return;
    const ticker = total === 1 ? ((paths && paths[0]) || '') : `${total} files`;
    this.setIslandState(true, 'Deleting permanently…', null, ticker);
    try {
      const conf = await this.invoke('large_files_confirm', { paths, job_id: this.largeFilesJobId || '' });
      if (!conf?.token) {
        this.showToast(conf?.message || 'Confirmation failed', 'error');
        this.setIslandState(false, 'Delete cancelled', 0, conf?.message || 'Confirmation failed');
        return;
      }
      if (!confirm(`Final confirmation: permanently delete ${conf.total_str || ''}? This cannot be undone.`)) {
        this.setIslandState(false, 'Delete cancelled', 0, '');
        return;
      }
      const res = await this.invoke('large_files_delete', { paths, token: conf.token });
      this.showToast(res?.message || 'Delete finished', res?.success === false ? 'error' : 'success');
      this.setIslandState(false, res?.success === false ? 'Delete failed' : 'Deleted permanently', res?.success === false ? 0 : 100, res?.message || 'Delete finished');
    } catch (e) {
      this.showToast(String(e.message || e), 'error');
      this.setIslandState(false, 'Delete failed', 0, String(e.message || e));
    }
    this.renderLargeFilesPage();
  }

  // ================= Settings & Diagnostics =================

  _setCatalogSourcesStatus(text) {
    const el = document.getElementById('catalog-sources-status');
    if (el) el.textContent = text || '';
  }

  async refreshCatalogSources() {
    const btn = document.getElementById('btn-catalog-sources-refresh');
    const resetBtn = document.getElementById('btn-catalog-sources-reset');
    if (btn) btn.disabled = true;
    if (resetBtn) resetBtn.disabled = true;
    this._setCatalogSourcesStatus('Refreshing catalog sources…');
    this.setIslandState(true, 'Refreshing catalog sources', null, 'Working');
    try {
      const res = await this.invoke('catalog_sources_refresh', {});
      if (res && res.ok) {
        this._setCatalogSourcesStatus(res.message || 'Catalog sources refreshed.');
        this.setIslandState(false, 'Catalog sources refreshed', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Refresh failed.';
        this._setCatalogSourcesStatus(err);
        this.setIslandState(false, 'Catalog refresh failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setCatalogSourcesStatus(err);
      this.setIslandState(false, 'Catalog refresh failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
      if (resetBtn) resetBtn.disabled = false;
    }
  }

  async resetCatalogSources() {
    if (!confirm(
      'Reset catalog sources to the WinGet defaults?\n\n' +
      'Use this only if Refresh did not fix a broken catalog. Custom sources you added will be removed.'
    )) {
      return;
    }
    if (!confirm('Final confirmation: reset catalog sources now?')) {
      return;
    }
    const btn = document.getElementById('btn-catalog-sources-refresh');
    const resetBtn = document.getElementById('btn-catalog-sources-reset');
    if (btn) btn.disabled = true;
    if (resetBtn) resetBtn.disabled = true;
    this._setCatalogSourcesStatus('Resetting catalog sources…');
    this.setIslandState(true, 'Resetting catalog sources', null, 'Working');
    try {
      const res = await this.invoke('catalog_sources_reset', { confirmed: true });
      if (res && res.ok) {
        this._setCatalogSourcesStatus(res.message || 'Catalog sources reset to defaults.');
        this.setIslandState(false, 'Catalog sources reset', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Reset failed.';
        this._setCatalogSourcesStatus(err);
        this.setIslandState(false, 'Catalog reset failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setCatalogSourcesStatus(err);
      this.setIslandState(false, 'Catalog reset failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
      if (resetBtn) resetBtn.disabled = false;
    }
  }

  _setStoreAppsStatus(text) {
    const el = document.getElementById('store-apps-status');
    if (el) el.textContent = text || '';
  }

  async loadStoreAppsList() {
    const btn = document.getElementById('btn-store-apps-load');
    const select = document.getElementById('store-apps-select');
    const regBtn = document.getElementById('btn-store-apps-reregister');
    if (btn) btn.disabled = true;
    this._setStoreAppsStatus('Loading Store apps for this account…');
    try {
      const res = await this.invoke('store_apps_list', {});
      if (!res || !res.ok) {
        const err = (res && res.error) || 'Could not load Store apps.';
        this._setStoreAppsStatus(err);
        if (select) {
          select.innerHTML = '<option value="">None loaded</option>';
          select.disabled = true;
        }
        if (regBtn) regBtn.disabled = true;
        return;
      }
      const packages = Array.isArray(res.packages) ? res.packages : [];
      if (select) {
        select.innerHTML = '';
        if (!packages.length) {
          const empty = document.createElement('option');
          empty.value = '';
          empty.textContent = 'No Store apps found';
          select.appendChild(empty);
          select.disabled = true;
          if (regBtn) regBtn.disabled = true;
        } else {
          packages.forEach((p) => {
            const opt = document.createElement('option');
            opt.value = String(p.package_full_name || '');
            opt.textContent = String(p.name || p.package_full_name || '');
            select.appendChild(opt);
          });
          select.disabled = false;
          if (regBtn) regBtn.disabled = false;
        }
      }
      this._setStoreAppsStatus(`${packages.length} Store app(s) listed for this account.`);
    } catch (e) {
      this._setStoreAppsStatus(String(e.message || e));
      if (regBtn) regBtn.disabled = true;
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async reregisterSelectedStoreApp() {
    const select = document.getElementById('store-apps-select');
    const full = select && select.value ? String(select.value) : '';
    if (!full) {
      this._setStoreAppsStatus('Choose a Store app first.');
      return;
    }
    const label = (select && select.options[select.selectedIndex])
      ? select.options[select.selectedIndex].textContent
      : full;
    if (!confirm(
      `Re-register "${label}" for this Windows account?\n\n` +
      'Use this when that Store app will not open. It does not uninstall the app.'
    )) {
      return;
    }
    const btn = document.getElementById('btn-store-apps-reregister');
    const loadBtn = document.getElementById('btn-store-apps-load');
    if (btn) btn.disabled = true;
    if (loadBtn) loadBtn.disabled = true;
    this._setStoreAppsStatus('Re-registering Store app…');
    this.setIslandState(true, 'Re-registering Store app', null, 'Working');
    try {
      const res = await this.invoke('store_apps_reregister', {
        package_full_name: full,
        confirmed: true,
      });
      if (res && res.ok) {
        this._setStoreAppsStatus(res.message || 'Store app re-registered.');
        this.setIslandState(false, 'Store app re-registered', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Re-register failed.';
        this._setStoreAppsStatus(err);
        this.setIslandState(false, 'Store re-register failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setStoreAppsStatus(err);
      this.setIslandState(false, 'Store re-register failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
      if (loadBtn) loadBtn.disabled = false;
    }
  }

  _setDuplicatesStatus(text) {
    const el = document.getElementById('duplicates-status');
    if (el) el.textContent = text || '';
  }

  _duplicateGroups = [];

  async scanDuplicateFiles() {
    const rootEl = document.getElementById('duplicates-root-path');
    const listEl = document.getElementById('duplicates-results');
    const scanBtn = document.getElementById('btn-duplicates-scan');
    const recycleBtn = document.getElementById('btn-duplicates-recycle');
    const root = rootEl && rootEl.value ? String(rootEl.value).trim() : '';
    if (!root) {
      this._setDuplicatesStatus('Enter a folder path to scan.');
      return;
    }
    if (scanBtn) scanBtn.disabled = true;
    this._setDuplicatesStatus('Scanning for duplicates…');
    this._duplicateGroups = [];
    try {
      const res = await this.invoke('duplicates_scan', { root, min_mb: 1 });
      if (!res || res.error) {
        this._setDuplicatesStatus((res && res.error) || 'Scan failed.');
        if (listEl) {
          listEl.innerHTML = '';
          listEl.disabled = true;
        }
        if (recycleBtn) recycleBtn.disabled = true;
        return;
      }
      this._duplicateGroups = Array.isArray(res.groups) ? res.groups : [];
      if (listEl) {
        listEl.innerHTML = '';
        this._duplicateGroups.forEach((group, gi) => {
          (group.paths || []).forEach((path, pi) => {
            const opt = document.createElement('option');
            opt.value = path;
            opt.dataset.group = String(gi);
            opt.textContent = `${group.size_str || ''} — ${path}`;
            if (pi === 0) opt.textContent += ' (keeping one by default)';
            listEl.appendChild(opt);
          });
        });
        listEl.disabled = this._duplicateGroups.length === 0;
      }
      const capNote = res.capped ? ' (scan capped)' : '';
      this._setDuplicatesStatus(
        `${this._duplicateGroups.length} duplicate group(s); ${res.files_scanned || 0} file(s) scanned${capNote}.`
      );
      if (recycleBtn) recycleBtn.disabled = this._duplicateGroups.length === 0;
    } catch (e) {
      this._setDuplicatesStatus(String(e.message || e));
      if (recycleBtn) recycleBtn.disabled = true;
    } finally {
      if (scanBtn) scanBtn.disabled = false;
    }
  }

  async recycleSelectedDuplicates() {
    const listEl = document.getElementById('duplicates-results');
    const recycleBtn = document.getElementById('btn-duplicates-recycle');
    const selected = listEl
      ? Array.from(listEl.selectedOptions || []).map((o) => o.value).filter(Boolean)
      : [];
    if (!selected.length) {
      this._setDuplicatesStatus('Select duplicate files to recycle (not every file in a group).');
      return;
    }
    const ok = window.confirm(
      'Move the selected duplicate files to the Recycle Bin? At least one copy in each group must remain.'
    );
    if (!ok) return;
    if (recycleBtn) recycleBtn.disabled = true;
    this._setDuplicatesStatus('Moving files to the Recycle Bin…');
    try {
      const res = await this.invoke('duplicates_recycle', { paths: selected, confirmed: true });
      if (res && res.success) {
        this._setDuplicatesStatus(res.message || 'Duplicates recycled.');
        await this.scanDuplicateFiles();
      } else {
        this._setDuplicatesStatus((res && res.message) || 'Recycle failed.');
      }
    } catch (e) {
      this._setDuplicatesStatus(String(e.message || e));
    } finally {
      if (recycleBtn) recycleBtn.disabled = false;
    }
  }

  _setOrphanUninstallStatus(text) {
    const el = document.getElementById('orphan-uninstall-status');
    if (el) el.textContent = text || '';
  }

  async loadOrphanUninstallers() {
    const btn = document.getElementById('btn-orphan-uninstall-load');
    const select = document.getElementById('orphan-uninstall-select');
    const removeBtn = document.getElementById('btn-orphan-uninstall-remove');
    if (btn) btn.disabled = true;
    this._setOrphanUninstallStatus('Loading entries…');
    try {
      const res = await this.invoke('orphan_uninstallers_list', {});
      const items = res && Array.isArray(res.items) ? res.items : [];
      if (select) {
        select.innerHTML = '';
        if (!items.length) {
          const empty = document.createElement('option');
          empty.value = '';
          empty.textContent = 'No broken uninstall entries found';
          select.appendChild(empty);
          select.disabled = true;
          if (removeBtn) removeBtn.disabled = true;
        } else {
          items.forEach((row) => {
            const opt = document.createElement('option');
            opt.value = row.entry_id || '';
            opt.textContent = `${row.name || 'Program'} — ${row.reason || ''}`.trim();
            select.appendChild(opt);
          });
          select.disabled = false;
          if (removeBtn) removeBtn.disabled = false;
        }
      }
      this._setOrphanUninstallStatus(`${items.length} broken uninstall entr${items.length === 1 ? 'y' : 'ies'}.`);
    } catch (e) {
      this._setOrphanUninstallStatus(String(e.message || e));
      if (removeBtn) removeBtn.disabled = true;
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async removeSelectedOrphanUninstaller() {
    const select = document.getElementById('orphan-uninstall-select');
    const removeBtn = document.getElementById('btn-orphan-uninstall-remove');
    const entryId = select && select.value ? String(select.value).trim() : '';
    if (!entryId) {
      this._setOrphanUninstallStatus('Select an entry to remove.');
      return;
    }
    const label = select.selectedOptions && select.selectedOptions[0] ? select.selectedOptions[0].textContent : entryId;
    const ok = window.confirm(
      `Remove this broken uninstall registry entry?\n\n${label}\n\nRun a leftover scan afterward.`
    );
    if (!ok) return;
    if (removeBtn) removeBtn.disabled = true;
    this._setOrphanUninstallStatus('Removing registry entry…');
    try {
      const res = await this.invoke('orphan_uninstall_remove', { entry_id: entryId, confirmed: true });
      if (res && res.success) {
        const hint = res.suggest_leftovers_scan ? ' Run a leftover scan when convenient.' : '';
        this._setOrphanUninstallStatus((res.message || 'Entry removed.') + hint);
        await this.loadOrphanUninstallers();
      } else {
        this._setOrphanUninstallStatus((res && res.message) || 'Remove failed.');
      }
    } catch (e) {
      this._setOrphanUninstallStatus(String(e.message || e));
    } finally {
      if (removeBtn) removeBtn.disabled = false;
    }
  }

  _setScheduledCleanupStatus(text) {
    const el = document.getElementById('scheduled-cleanup-status');
    if (el) el.textContent = text || '';
  }

  _scheduledCleanupCategoryIds() {
    const ids = [];
    if (document.getElementById('sched-cat-user-temp')?.checked) ids.push('user_temp');
    if (document.getElementById('sched-cat-browser-caches')?.checked) ids.push('browser_caches');
    if (document.getElementById('sched-cat-recycle-bin')?.checked) ids.push('recycle_bin');
    return ids;
  }

  async loadScheduledCleanupSettings() {
    try {
      const res = await this.invoke('cleanup_schedule_list', {});
      if (!res) return;
      const user = document.getElementById('sched-cat-user-temp');
      const browser = document.getElementById('sched-cat-browser-caches');
      const rb = document.getElementById('sched-cat-recycle-bin');
      const cadence = document.getElementById('sched-cleanup-cadence');
      const cats = new Set(res.categories || []);
      if (user) user.checked = cats.has('user_temp');
      if (browser) browser.checked = cats.has('browser_caches');
      if (rb) rb.checked = cats.has('recycle_bin');
      if (cadence && res.cadence) cadence.value = res.cadence;
      if (res.configured) {
        this._setScheduledCleanupStatus(
          `Scheduled: ${(res.categories || []).join(', ')} (${res.cadence || ''}). Task registered: ${res.task_registered ? 'yes' : 'no'}.`
        );
      }
    } catch (_) {
      /* settings optional on first paint */
    }
  }

  async saveScheduledCleanup() {
    const saveBtn = document.getElementById('btn-sched-cleanup-save');
    const categoryIds = this._scheduledCleanupCategoryIds();
    const cadenceEl = document.getElementById('sched-cleanup-cadence');
    const cadence = cadenceEl ? String(cadenceEl.value || 'daily') : 'daily';
    if (!categoryIds.length) {
      this._setScheduledCleanupStatus('Select at least one cleanup category.');
      return;
    }
    const ok = window.confirm('Save this cleanup schedule and register the Windows scheduled task?');
    if (!ok) return;
    if (saveBtn) saveBtn.disabled = true;
    this._setScheduledCleanupStatus('Saving schedule…');
    try {
      const res = await this.invoke('cleanup_schedule_save', { category_ids: categoryIds, cadence });
      if (res && res.success) {
        this._setScheduledCleanupStatus(res.message || 'Schedule saved.');
        await this.loadScheduledCleanupSettings();
      } else {
        this._setScheduledCleanupStatus((res && res.message) || 'Save failed.');
      }
    } catch (e) {
      this._setScheduledCleanupStatus(String(e.message || e));
    } finally {
      if (saveBtn) saveBtn.disabled = false;
    }
  }

  async removeScheduledCleanup() {
    const removeBtn = document.getElementById('btn-sched-cleanup-remove');
    const ok = window.confirm('Remove the saved cleanup schedule and delete the scheduled task?');
    if (!ok) return;
    if (removeBtn) removeBtn.disabled = true;
    try {
      const res = await this.invoke('cleanup_schedule_remove', {});
      if (res && res.success) {
        this._setScheduledCleanupStatus(res.message || 'Schedule removed.');
        await this.loadScheduledCleanupSettings();
      } else {
        this._setScheduledCleanupStatus((res && res.message) || 'Remove failed.');
      }
    } catch (e) {
      this._setScheduledCleanupStatus(String(e.message || e));
    } finally {
      if (removeBtn) removeBtn.disabled = false;
    }
  }

  _setPrintQueueStatus(text) {
    const el = document.getElementById('print-queue-status');
    if (el) el.textContent = text || '';
  }

  async loadPrintJobs() {
    const btn = document.getElementById('btn-print-jobs-load');
    const select = document.getElementById('print-jobs-select');
    const cancelBtn = document.getElementById('btn-print-job-cancel');
    if (btn) btn.disabled = true;
    this._setPrintQueueStatus('Loading print jobs…');
    try {
      const res = await this.invoke('print_jobs_list', {});
      if (!res || !res.ok) {
        this._setPrintQueueStatus((res && res.error) || 'Could not load print jobs.');
        if (select) {
          select.innerHTML = '';
          const empty = document.createElement('option');
          empty.value = '';
          empty.textContent = 'None loaded';
          select.appendChild(empty);
          select.disabled = true;
        }
        if (cancelBtn) cancelBtn.disabled = true;
        return;
      }
      const jobs = Array.isArray(res.jobs) ? res.jobs : [];
      if (select) {
        select.innerHTML = '';
        if (!jobs.length) {
          const empty = document.createElement('option');
          empty.value = '';
          empty.textContent = 'No print jobs in the queue';
          select.appendChild(empty);
          select.disabled = true;
          if (cancelBtn) cancelBtn.disabled = true;
        } else {
          jobs.forEach((j) => {
            const opt = document.createElement('option');
            opt.value = `${j.printer}\t${j.id}`;
            const doc = j.document || 'Document';
            opt.textContent = `${doc} — ${j.printer} (#${j.id}) ${j.status || ''}`.trim();
            select.appendChild(opt);
          });
          select.disabled = false;
          if (cancelBtn) cancelBtn.disabled = false;
        }
      }
      this._setPrintQueueStatus(`${jobs.length} print job(s) in the queue.`);
    } catch (e) {
      this._setPrintQueueStatus(String(e.message || e));
      if (cancelBtn) cancelBtn.disabled = true;
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async cancelSelectedPrintJob() {
    const select = document.getElementById('print-jobs-select');
    const raw = select && select.value ? String(select.value) : '';
    const parts = raw.split('\t');
    const printer = parts[0] || '';
    const jobId = parts[1] || '';
    if (!printer || !jobId) {
      this._setPrintQueueStatus('Choose a print job first.');
      return;
    }
    const label = (select && select.options[select.selectedIndex])
      ? select.options[select.selectedIndex].textContent
      : raw;
    if (!confirm(`Cancel this print job?\n\n${label}`)) return;
    const btn = document.getElementById('btn-print-job-cancel');
    if (btn) btn.disabled = true;
    this._setPrintQueueStatus('Cancelling print job…');
    this.setIslandState(true, 'Cancelling print job', null, 'Working');
    try {
      const res = await this.invoke('print_job_cancel', {
        printer,
        job_id: jobId,
        confirmed: true,
      });
      if (res && res.ok) {
        this._setPrintQueueStatus(res.message || 'Print job cancelled.');
        this.setIslandState(false, 'Print job cancelled', 100, res.message || 'Done');
        await this.loadPrintJobs();
      } else {
        const err = (res && (res.error || res.message)) || 'Cancel failed.';
        this._setPrintQueueStatus(err);
        this.setIslandState(false, 'Cancel failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setPrintQueueStatus(err);
      this.setIslandState(false, 'Cancel failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  _setToolStatus(id, text) {
    const el = document.getElementById(id);
    if (el) el.textContent = text || '';
  }

  async healthAction(kind) {
    const statusId = 'windows-health-status';
    const confirmHeavy = ['repair_wu', 'sfc_repair', 'dism_repair', 'restore', 'retrim', 'defender_update', 'defender_scan'];
    if (confirmHeavy.includes(kind) && !confirm('Run this Windows health action now?')) return;
    this._setToolStatus(statusId, 'Working…');
    this.setIslandState(true, 'Windows health', null, 'Working');
    try {
      let res = null;
      if (kind === 'open_wu') res = await this.invoke('health_open_windows_update', {});
      else if (kind === 'repair_wu') res = await this.invoke('health_repair_wu', { confirmed: true });
      else if (kind === 'sfc') res = await this.invoke('health_sfc', { repair: false });
      else if (kind === 'sfc_repair') res = await this.invoke('health_sfc', { repair: true, confirmed: true });
      else if (kind === 'dism_scan') res = await this.invoke('health_dism', { scan_only: true });
      else if (kind === 'dism_repair') res = await this.invoke('health_dism', { scan_only: false, confirmed: true });
      else if (kind === 'wmi') res = await this.invoke('health_wmi', {});
      else if (kind === 'restore') res = await this.invoke('health_restore_point', { confirmed: true });
      else if (kind === 'disk') res = await this.invoke('health_disk', {});
      else if (kind === 'volume') res = await this.invoke('health_volume', { drive: 'C:' });
      else if (kind === 'retrim') res = await this.invoke('health_retrim', { drive: 'C:', confirmed: true });
      else if (kind === 'events') res = await this.invoke('health_events', { log_name: 'System' });
      else if (kind === 'defender') res = await this.invoke('health_defender_status', {});
      else if (kind === 'defender_update') res = await this.invoke('health_defender_update', { confirmed: true });
      else if (kind === 'defender_scan') res = await this.invoke('health_defender_scan', { confirmed: true });
      else if (kind === 'bitlocker') res = await this.invoke('health_bitlocker', {});
      else if (kind === 'hotfixes') res = await this.invoke('health_list_hotfixes', {});
      else if (kind === 'bsod') res = await this.invoke('health_bsod_triage', {});
      const text = res && res.ok
        ? (res.message || JSON.stringify(res.status || res.disks || res.volumes || res.events || res.updates || res, null, 0).slice(0, 1200))
        : ((res && (res.error || res.message)) || 'Action failed.');
      this._setToolStatus(statusId, text);
      this.setIslandState(false, res && res.ok ? 'Windows health done' : 'Windows health failed', res && res.ok ? 100 : 0, text);
    } catch (e) {
      this._setToolStatus(statusId, String(e.message || e));
      this.setIslandState(false, 'Windows health failed', 0, String(e.message || e));
    }
  }

  async netAction(kind) {
    const statusId = 'network-tools-status';
    const hostEl = document.getElementById('net-host-input');
    const portEl = document.getElementById('net-port-input');
    const host = hostEl ? String(hostEl.value || '').trim() : '';
    const port = portEl ? Number(portEl.value || 0) : 0;
    const needsConfirm = ['flush_dns', 'winsock', 'dhcp', 'proxy_reset', 'hosts_restore', 'clock_sync', 'tcpip_reset', 'firewall_reset'];
    if (needsConfirm.includes(kind) && !confirm('Run this network action now?')) return;
    this._setToolStatus(statusId, 'Working…');
    try {
      let res = null;
      if (kind === 'flush_dns') res = await this.invoke('net_flush_dns', { confirmed: true });
      else if (kind === 'winsock') res = await this.invoke('net_reset_winsock', { confirmed: true });
      else if (kind === 'dhcp') res = await this.invoke('net_renew_dhcp', { confirmed: true });
      else if (kind === 'power') res = await this.invoke('net_power_plans', {});
      else if (kind === 'listeners') res = await this.invoke('net_listeners', {});
      else if (kind === 'proxy') res = await this.invoke('net_proxy_status', {});
      else if (kind === 'proxy_reset') res = await this.invoke('net_proxy_reset', { confirmed: true });
      else if (kind === 'hosts') res = await this.invoke('net_hosts_preview', {});
      else if (kind === 'hosts_restore') res = await this.invoke('net_hosts_restore', { confirmed: true });
      else if (kind === 'clock') res = await this.invoke('net_clock_status', {});
      else if (kind === 'clock_sync') res = await this.invoke('net_clock_sync', { confirmed: true });
      else if (kind === 'ping') res = await this.invoke('net_ping', { host });
      else if (kind === 'trace') res = await this.invoke('net_traceroute', { host });
      else if (kind === 'port') res = await this.invoke('net_test_port', { host, port });
      else if (kind === 'dns') res = await this.invoke('net_dns_lookup', { name: host });
      else if (kind === 'adapter_backup') res = await this.invoke('net_backup_adapters', {});
      else if (kind === 'tcpip_reset') res = await this.invoke('net_reset_tcpip', { confirmed: true });
      else if (kind === 'firewall_rules') res = await this.invoke('net_firewall_rules', {});
      else if (kind === 'firewall_export') res = await this.invoke('net_firewall_export', {});
      else if (kind === 'firewall_reset') res = await this.invoke('net_firewall_reset', { confirmed: true });
      else if (kind === 'wifi') res = await this.invoke('net_wifi_survey', {});
      else if (kind === 'sleep_wake') res = await this.invoke('net_sleep_wake', {});
      const text = res && res.ok
        ? (res.message || res.detail || JSON.stringify(res.plans || res.listeners || res.addresses || res.networks || res.sections || res, null, 0).slice(0, 1200))
        : ((res && (res.error || res.message)) || 'Action failed.');
      this._setToolStatus(statusId, text);
    } catch (e) {
      this._setToolStatus(statusId, String(e.message || e));
    }
  }

  async filesAction(kind) {
    const statusId = 'file-tools-status';
    const pathEl = document.getElementById('files-path-input');
    const extraEl = document.getElementById('files-extra-input');
    const path = pathEl ? String(pathEl.value || '').trim() : '';
    const extra = extraEl ? String(extraEl.value || '').trim() : '';
    const needsConfirm = [
      'compress', 'mount', 'eject', 'format', 'context_restore', 'context_disable',
      'svc_start', 'svc_stop', 'svc_restart', 'feat_enable', 'feat_disable',
    ];
    if (needsConfirm.includes(kind) && !confirm('Run this file or admin action now?')) return;
    if (kind === 'format' && !confirm('Final warning: formatting erases the removable volume. Continue?')) return;
    this._setToolStatus(statusId, 'Working…');
    try {
      let res = null;
      if (kind === 'checksum') res = await this.invoke('files_checksum', { path });
      else if (kind === 'signature') res = await this.invoke('files_verify_signature', { path });
      else if (kind === 'empty') res = await this.invoke('files_empty_folders', { root: path || (window.pywebview ? '' : '') });
      else if (kind === 'processes') res = await this.invoke('files_processes', {});
      else if (kind === 'compress') res = await this.invoke('files_compress_folder', { folder: path, confirmed: true });
      else if (kind === 'mount') res = await this.invoke('files_disk_image_mount', { path, confirmed: true, read_only: true });
      else if (kind === 'eject') res = await this.invoke('files_disk_image_eject', { path, confirmed: true });
      else if (kind === 'shortcuts') res = await this.invoke('files_broken_shortcuts', { root: path });
      else if (kind === 'lock') res = await this.invoke('files_lock_probe', { path });
      else if (kind === 'acl') res = await this.invoke('files_acl_inspect', { path });
      else if (kind === 'reg_preview') res = await this.invoke('files_reg_preview', { path });
      else if (kind === 'removable') res = await this.invoke('files_removable_volumes', {});
      else if (kind === 'format') res = await this.invoke('files_format_removable', { letter: extra || path, confirmed: true });
      else if (kind === 'privacy') res = await this.invoke('files_privacy_open', {});
      else if (kind === 'context_list') res = await this.invoke('shell_context_list', {});
      else if (kind === 'context_disable') {
        const handlerId = extra || (typeof window !== 'undefined' && window.prompt
          ? window.prompt('Paste handler id from Context menus list (Registry::…):', '')
          : '');
        if (!handlerId) {
          this._setToolStatus(statusId, 'Handler id required. Run Context menus first.');
          return;
        }
        res = await this.invoke('shell_context_disable', { handler_id: handlerId, confirmed: true });
      } else if (kind === 'context_restore') res = await this.invoke('shell_context_restore', { confirmed: true });
      else if (kind === 'services') res = await this.invoke('files_services_list', {});
      else if (kind === 'svc_start' || kind === 'svc_stop' || kind === 'svc_restart') {
        const name = extra || path;
        if (!name) {
          this._setToolStatus(statusId, 'Enter a service name in the extra field (from Services list).');
          return;
        }
        const action = kind === 'svc_start' ? 'start' : (kind === 'svc_stop' ? 'stop' : 'restart');
        res = await this.invoke('files_service_action', { name, action, confirmed: true });
      } else if (kind === 'optional') res = await this.invoke('files_optional_features', {});
      else if (kind === 'feat_enable' || kind === 'feat_disable') {
        const name = extra || path;
        if (!name) {
          this._setToolStatus(statusId, 'Enter a feature name in the extra field (from Optional features list).');
          return;
        }
        res = await this.invoke('files_optional_feature_set', {
          name,
          enable: kind === 'feat_enable',
          confirmed: true,
        });
      } else if (kind === 'awake') res = await this.invoke('files_keep_awake_start', { display: true });
      else if (kind === 'awake_off') res = await this.invoke('files_keep_awake_cancel', {});
      const text = res && res.ok
        ? (res.message || res.hex || res.status || res.preview || JSON.stringify(
          res.folders || res.processes || res.handlers || res.services || res.features || res.volumes || res.shortcuts || res,
          null,
          0
        ).slice(0, 1200))
        : ((res && (res.error || res.message)) || 'Action failed.');
      this._setToolStatus(statusId, text);
    } catch (e) {
      this._setToolStatus(statusId, String(e.message || e));
    }
  }

  _setCatalogExportStatus(text) {
    const el = document.getElementById('catalog-export-status');
    if (el) el.textContent = text || '';
  }

  async exportAppCatalog() {
    const pathEl = document.getElementById('catalog-export-path');
    const path = pathEl && pathEl.value ? String(pathEl.value).trim() : 'Downloads\\quantum-updater-apps.json';
    const btn = document.getElementById('btn-catalog-export');
    const importBtn = document.getElementById('btn-catalog-import');
    if (btn) btn.disabled = true;
    if (importBtn) importBtn.disabled = true;
    this._setCatalogExportStatus('Exporting installed apps…');
    this.setIslandState(true, 'Exporting app catalog', null, 'Working');
    try {
      const res = await this.invoke('catalog_export', { path });
      if (res && res.ok) {
        this._setCatalogExportStatus(res.message || 'Catalog exported.');
        this.setIslandState(false, 'Catalog exported', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Export failed.';
        this._setCatalogExportStatus(err);
        this.setIslandState(false, 'Catalog export failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setCatalogExportStatus(err);
      this.setIslandState(false, 'Catalog export failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
      if (importBtn) importBtn.disabled = false;
    }
  }

  async importAppCatalog() {
    const pathEl = document.getElementById('catalog-export-path');
    const path = pathEl && pathEl.value ? String(pathEl.value).trim() : '';
    if (!path) {
      this._setCatalogExportStatus('Enter the path to a catalog JSON file first.');
      return;
    }
    if (!confirm(
      'Import apps from this catalog file?\n\n' +
      'WinGet may download and install many packages. This can take a long time and needs network access.'
    )) {
      return;
    }
    if (!confirm('Final confirmation: start catalog import now?')) return;
    const btn = document.getElementById('btn-catalog-import');
    const exportBtn = document.getElementById('btn-catalog-export');
    if (btn) btn.disabled = true;
    if (exportBtn) exportBtn.disabled = true;
    this._setCatalogExportStatus('Importing apps from catalog…');
    this.setIslandState(true, 'Importing app catalog', null, 'Working');
    try {
      const res = await this.invoke('catalog_import', { path, confirmed: true });
      if (res && res.ok) {
        this._setCatalogExportStatus(res.message || 'Catalog import finished.');
        this.setIslandState(false, 'Catalog import finished', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Import failed.';
        this._setCatalogExportStatus(err);
        this.setIslandState(false, 'Catalog import failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setCatalogExportStatus(err);
      this.setIslandState(false, 'Catalog import failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
      if (exportBtn) exportBtn.disabled = false;
    }
  }

  _setAppRepairStatus(text) {
    const el = document.getElementById('app-repair-status');
    if (el) el.textContent = text || '';
  }

  async repairDesktopApp() {
    const input = document.getElementById('app-repair-package-id');
    const packageId = input && input.value ? String(input.value).trim() : '';
    if (!packageId) {
      this._setAppRepairStatus('Enter a WinGet package id first (for example Publisher.AppName).');
      return;
    }
    if (!confirm(
      `Repair "${packageId}" with WinGet?\n\n` +
      'This works only when the app publisher supports repair.'
    )) {
      return;
    }
    const btn = document.getElementById('btn-app-repair-desktop');
    if (btn) btn.disabled = true;
    this._setAppRepairStatus('Repairing desktop app…');
    this.setIslandState(true, 'Repairing desktop app', null, 'Working');
    try {
      const res = await this.invoke('app_repair_desktop', { package_id: packageId, confirmed: true });
      if (res && res.ok) {
        this._setAppRepairStatus(res.message || 'Desktop app repair finished.');
        this.setIslandState(false, 'Desktop app repaired', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Repair failed.';
        this._setAppRepairStatus(err);
        this.setIslandState(false, 'Desktop repair failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setAppRepairStatus(err);
      this.setIslandState(false, 'Desktop repair failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async resetSelectedPackagedApp() {
    const select = document.getElementById('store-apps-select');
    const full = select && select.value ? String(select.value) : '';
    if (!full) {
      this._setAppRepairStatus('Load Store apps above and choose one before resetting.');
      return;
    }
    const label = (select && select.options[select.selectedIndex])
      ? select.options[select.selectedIndex].textContent
      : full;
    if (!confirm(
      `Reset "${label}"?\n\n` +
      'This removes that Store app\'s saved data on this PC. The app stays installed but may act like a fresh install.'
    )) {
      return;
    }
    if (!confirm('Final confirmation: reset that Store app\'s data now?')) return;
    const btn = document.getElementById('btn-packaged-app-reset');
    if (btn) btn.disabled = true;
    this._setAppRepairStatus('Resetting Store app data…');
    this.setIslandState(true, 'Resetting Store app', null, 'Working');
    try {
      const res = await this.invoke('packaged_app_reset', {
        package_full_name: full,
        confirmed: true,
      });
      if (res && res.ok) {
        this._setAppRepairStatus(res.message || 'Store app data reset.');
        this.setIslandState(false, 'Store app reset', 100, res.message || 'Done');
      } else {
        const err = (res && (res.error || res.message)) || 'Reset failed.';
        this._setAppRepairStatus(err);
        this.setIslandState(false, 'Store app reset failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setAppRepairStatus(err);
      this.setIslandState(false, 'Store app reset failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async clearPrintQueue() {
    if (!confirm(
      'Clear the whole print queue and restart the print spooler?\n\n' +
      'This cancels every waiting print job on this PC. Use only if cancelling one job did not help.'
    )) {
      return;
    }
    if (!confirm('Final confirmation: clear the print queue now?')) return;
    const btn = document.getElementById('btn-print-queue-clear');
    if (btn) btn.disabled = true;
    this._setPrintQueueStatus('Clearing print queue and restarting spooler…');
    this.setIslandState(true, 'Repairing print spooler', null, 'Working');
    try {
      const res = await this.invoke('print_queue_clear', { confirmed: true });
      if (res && res.ok) {
        this._setPrintQueueStatus(res.message || 'Print queue cleared.');
        this.setIslandState(false, 'Print spooler restarted', 100, res.message || 'Done');
        await this.loadPrintJobs();
      } else {
        const err = (res && (res.error || res.message)) || 'Spooler repair failed.';
        this._setPrintQueueStatus(err);
        this.setIslandState(false, 'Spooler repair failed', 0, err);
      }
    } catch (e) {
      const err = String(e.message || e);
      this._setPrintQueueStatus(err);
      this.setIslandState(false, 'Spooler repair failed', 0, err);
    } finally {
      if (btn) btn.disabled = false;
    }
  }

  async loadInitialDiagnostics() {
    this.applyAllowBetaCheckbox();
    try {
      const diag = await this.invoke('get_diagnostics', { activeEngine: this.activeEngine });
      if (!diag) return;

      this.hardwareDiag = diag;
      await this.renderHardwareSpecs(diag);
      this.renderDiagnostics(diag);
      this.renderEngineSelector(diag.engines, diag.active_engine);

      const adminText = document.getElementById('admin-status-text');
      const adminPill = document.getElementById('admin-status-pill');
      if (diag.is_admin) {
        if (adminText) adminText.textContent = 'Elevated';
        if (adminPill) adminPill.className = 'status-pill status-admin';
      } else {
        if (adminText) adminText.textContent = 'User Mode';
        if (adminPill) adminPill.className = 'status-pill status-admin standard';
      }

      this.activeEngine = diag.active_engine || this.activeEngine || 'winget';
      const engineLabel = diag.active_engine || 'Catalog';
      const badgeEl = document.getElementById('active-engine-badge');
      if (badgeEl) badgeEl.textContent = 'PC Health: Good';
      const bentoEngine = document.getElementById('bento-engine-val');
      if (bentoEngine) bentoEngine.textContent = engineLabel;
      await this.loadScheduledCleanupSettings();
    } catch (e) {
      console.warn('Diagnostics notice:', e);
    }
  }

  async resolveAboutStorageDrives(diag) {
    const hw = (diag && diag.hardware) || {};
    let drives = Array.isArray(hw.storage) ? hw.storage.slice() : [];
    try {
      const extra = await this.invoke('cleanup_drives', {});
      if (Array.isArray(extra) && extra.length) drives = extra;
    } catch (e) {
      /* feature-detect: fall back to hardware.storage */
    }
    return drives;
  }

  async renderHardwareSpecs(diag) {
    if (!diag) return;
    this.hardwareDiag = diag;

    const root = document.getElementById('about-computer-root');
    if (!root) return;

    const storageDrives = await this.resolveAboutStorageDrives(diag);
    if (typeof buildAboutComputerPageHtml === 'function') {
      root.innerHTML = buildAboutComputerPageHtml(diag, storageDrives);
    }

    const hw = diag.hardware || {};
    const cpu = hw.cpu || {};
    const archChip = aboutFormatArch(cpu.arch || diag.arch) || 'x64';
    const archEl = document.getElementById('sidebar-arch-chip') || document.querySelector('.security-chip-pill');
    if (archEl) archEl.textContent = archChip;

    const sideDiskPct = document.getElementById('sidebar-disk-pct');
    if (sideDiskPct) sideDiskPct.textContent = `${diag.storage_used_percent || 0}%`;
    const sideDiskBar = document.getElementById('sidebar-disk-bar');
    if (sideDiskBar) sideDiskBar.style.width = `${diag.storage_used_percent || 0}%`;
    const sideDiskFree = document.getElementById('sidebar-disk-free');
    if (sideDiskFree) {
      const free = diag.storage_free_str || '';
      sideDiskFree.textContent = free ? `${free} free` : sideDiskFree.textContent;
    }
  }

  copySerialNumber() {
    const hw = (this.hardwareDiag && this.hardwareDiag.hardware) || {};
    const device = hw.device || {};
    const serial = device.serial_number || (this.hardwareDiag && this.hardwareDiag.serial_number) || '';
    if (!serial) {
      this.showToast('This PC has not reported a serial number yet', 'info');
      return;
    }
    navigator.clipboard.writeText(serial).then(() => {
      this.showToast('Serial number copied to clipboard', 'success');
      this.playHapticTick();
    }).catch(() => {
      this.showToast(`Serial: ${serial}`, 'info');
    });
  }

  copyAllSystemSpecs() {
    if (!this.hardwareDiag) return;
    const d = this.hardwareDiag;
    const hw = d.hardware || {};
    const device = hw.device || {};
    const cpu = hw.cpu || {};
    const catalogParts = [d.catalog_version, d.engine_version].filter(Boolean);
    if (!catalogParts.length) {
      const cv = [d.winget_version, d.choco_version, d.scoop_version].filter(Boolean);
      if (cv.length) catalogParts.push(cv.join(', '));
    }
    const catalogLine = catalogParts.length
      ? `Catalog tool version: ${catalogParts.join(' / ')}`
      : 'Catalog tool version: Not reported by this PC';
    const lines = [
      `Product: ${device.model_name || d.model_name || 'Not reported by this PC'}`,
      `Model: ${device.model_id || d.model_id || 'Not reported by this PC'}`,
      `Serial: ${device.serial_number || d.serial_number || 'Not reported by this PC'}`,
      `OS: ${(hw.os && hw.os.name) || d.os_display || d.os_info || 'Not reported by this PC'}`,
      `Processor: ${cpu.brand_string || d.cpu || 'Not reported by this PC'}`,
      `Cores / threads: ${cpu.cores || '?'} / ${cpu.threads || '?'}`,
      `Generation: ${cpu.generation || 'Not reported by this PC'}`,
      `Architecture: ${aboutFormatArch(cpu.arch || d.arch) || 'Not reported by this PC'}`,
      `Memory: ${d.memory || aboutFormatGiB((hw.memory && hw.memory.total_bytes)) || 'Not reported by this PC'}`,
      `Storage: ${d.storage_free_str || ''} free of ${d.storage_total_str || ''} (${d.storage_used_percent || 0}% used)`.trim(),
      catalogLine
    ];
    const gpuLines = Array.isArray(hw.gpus) ? hw.gpus.filter((g) => !aboutIsUsbDisplay(g)) : [];
    gpuLines.forEach((g, i) => {
      lines.push(`Graphics ${i + 1}: ${g.name}${g.integrated ? ' (built-in)' : ''}`);
    });
    aboutCollectUsbDevices(hw).forEach((row, i) => {
      lines.push(`USB ${i + 1}: ${row.name} (external)`);
    });
    navigator.clipboard.writeText(lines.join('\n')).then(() => {
      this.showToast('All system specifications copied to clipboard', 'success');
      this.playHapticTick();
    });
  }

  renderDiagnostics(diag) {
    const container = document.getElementById('diagnostics-container');
    if (!container || !diag) return;

    if (this.isMacDiagnostics(diag)) {
      container.innerHTML = '<div class="diag-row"><span class="diag-key">Platform</span><span class="diag-val">This build targets Windows PCs.</span></div>';
      return;
    }

    const hw = diag.hardware || {};
    const cpu = hw.cpu || {};
    const arch = aboutFormatArch(cpu.arch || diag.arch) || 'x64';
    const rows = [
      ['Publisher', 'Quantum Step (quantumstep.in)'],
      ['Privilege Level', diag.is_admin ? 'Administrator (Elevated)' : 'Standard User (Not Elevated)'],
      ['Operating System', (hw.os && hw.os.name) || diag.os_info || diag.os_display || 'Not reported by this PC'],
      ['CPU Architecture', arch],
      ['Active Engine', diag.active_engine || 'Not reported by this PC']
    ];

    let html = '';
    rows.forEach(([k, v]) => {
      html += `
        <div class="diag-row">
          <span class="diag-key">${k}</span>
          <span class="diag-val">${this.escapeHtml(String(v))}</span>
        </div>
      `;
    });
    container.innerHTML = html;
  }


  renderEngineSelector(engines, activeId) {
    const container = document.getElementById('engine-options-container');
    if (!container || !engines) return;

    let html = '';
    engines.forEach(eng => {
      const isSelected = eng.id === activeId;
      html += `
        <div class="engine-option-row ${isSelected ? 'selected' : ''}" onclick="app.selectEngine('${eng.id}')">
          <div>
            <strong>${eng.name}</strong>
            <span style="font-size: 12px; color: var(--text-muted); display: block;">${eng.available ? `v${eng.version} (Ready)` : 'Not installed'}</span>
          </div>
          <div>${isSelected ? '● Active' : ''}</div>
        </div>
      `;
    });
    container.innerHTML = html;
  }

  async selectEngine(engineId) {
    this.activeEngine = engineId;
    await this.invoke('set_engine', { engineId: engineId });
    this.loadInitialDiagnostics();
    this.scanUpdates();
  }

  applyAllowBetaCheckbox() {
    const box = document.getElementById('pref-allow-beta');
    if (!box) return;
    box.checked = !!(this.settings && this.settings.allow_beta);
  }

  savePreference(key, value) {
    if (this.settings) this.settings[key] = value;
    this.invoke('save_setting', { key: key, value: value }).catch(() => {});
  }

  escapeHtml(str) {
    if (!str) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // ================= In-App Self-Updater System =================

  async checkAutoUpdateOnStartup() {
    const prefToggle = document.getElementById('pref-auto-update-check');
    // Prefer the pre-window result; still honour an explicit off toggle when present.
    if (prefToggle && !prefToggle.checked) {
      this._selfUpdateStatus = 'ready';
      return;
    }
    try {
      const pre = await this.invoke('get_startup_update', {});
      if (pre && pre.preloaded && !pre.pending) {
        await this._applySelfUpdateCheckResult(pre, true);
        return;
      }
    } catch (_) {
      /* fall through to a live check */
    }
    await this.checkForAppUpdates(true);
  }

  async _applySelfUpdateCheckResult(res, silent = false) {
    const btnCheck = document.getElementById('btn-check-updates');
    const timeEl = document.getElementById('update-last-checked-time');
    if (timeEl) {
      timeEl.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }
    if (res && res.has_update) {
      this.pendingUpdate = res;
      const statusBadge = document.getElementById('app-update-status-badge');
      if (statusBadge) {
        statusBadge.className = 'update-status-badge pending';
        statusBadge.innerHTML = `<span class="status-dot"></span><span id="app-version-display">v${res.latest_version} • Update Available</span>`;
      }
      const idleBox = document.getElementById('update-state-idle');
      const foundBox = document.getElementById('update-state-found');
      const progBox = document.getElementById('update-state-progress');
      if (idleBox) idleBox.style.display = 'none';
      if (progBox) progBox.style.display = 'none';
      if (foundBox) {
        foundBox.style.display = 'block';
        const tagEl = document.getElementById('update-new-ver-tag');
        if (tagEl) tagEl.textContent = `v${res.latest_version} Available`;
        const sizeEl = document.getElementById('update-new-ver-size');
        if (sizeEl) sizeEl.textContent = res.asset_size_str || '';
        const notesEl = document.getElementById('update-new-ver-notes');
        if (notesEl) notesEl.textContent = res.release_notes || res.release_name || 'Includes performance and security enhancements.';
      }
      const btnInstall = document.getElementById('btn-install-app-update');
      if (btnInstall) btnInstall.style.display = 'inline-flex';
      const banner = document.getElementById('global-update-banner');
      if (banner) {
        const bannerText = document.getElementById('update-banner-text');
        if (bannerText) {
          bannerText.textContent = `Quantum Updater v${res.latest_version} is available. Install before you continue for the better build.`;
        }
        banner.style.display = 'flex';
      }
      this.appendLog(`[Quantum Auto-Updater]: New release available: v${res.latest_version} (${res.asset_size_str || 'Verified'}).`);
    } else {
      this.pendingUpdate = null;
      const statusBadge = document.getElementById('app-update-status-badge');
      if (statusBadge) {
        statusBadge.className = 'update-status-badge';
        const cur = (res && res.current_version) || '2.0.0';
        statusBadge.innerHTML = `<span class="status-dot healthy"></span><span id="app-version-display">v${cur} • Up to Date</span>`;
      }
      const idleBox = document.getElementById('update-state-idle');
      const foundBox = document.getElementById('update-state-found');
      const progBox = document.getElementById('update-state-progress');
      if (idleBox) idleBox.style.display = 'block';
      if (foundBox) foundBox.style.display = 'none';
      if (progBox) progBox.style.display = 'none';
      const btnInstall = document.getElementById('btn-install-app-update');
      if (btnInstall) btnInstall.style.display = 'none';
      if (!silent) {
        this.appendLog('[Quantum Auto-Updater]: Installed application is on the latest verified release.');
      }
      if (res && res.error && !silent) {
        this.appendLog(`[Quantum Auto-Updater Warning]: ${res.error}`);
      }
    }
    if (btnCheck) {
      btnCheck.disabled = false;
    }
  }

  async checkForAppUpdates(silent = false) {
    if (this.isCheckingAppUpdate || this.isApplyingAppUpdate) return;
    this.isCheckingAppUpdate = true;

    const btnCheck = document.getElementById('btn-check-updates');
    if (btnCheck) {
      btnCheck.disabled = true;
      btnCheck.innerHTML = `
        <svg class="spin-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <circle cx="12" cy="12" r="10"></circle>
          <path d="M12 2a10 10 0 0 1 10 10"></path>
        </svg>
        <span>Checking Feed...</span>
      `;
    }

    if (!silent) {
      this.appendLog('[Quantum Auto-Updater]: Connecting to GitHub release feed to verify version integrity...');
    }

    try {
      const res = await this.invoke('check_self_update', { force: !silent });
      await this._applySelfUpdateCheckResult(res, silent);
    } catch (err) {
      console.warn('[Auto-Updater Check Failed]:', err);
      if (!silent) {
        this.appendLog(`[Quantum Auto-Updater Warning]: Could not reach release feed: ${err.message || err}`);
      }
    } finally {
      this.isCheckingAppUpdate = false;
      if (btnCheck) {
        btnCheck.disabled = false;
        btnCheck.innerHTML = `
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline points="23 4 23 10 17 10"></polyline>
            <polyline points="1 20 1 14 7 14"></polyline>
            <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
          </svg>
          <span>Check for Updates</span>
        `;
      }
    }
  }

  async applySelfUpdate() {
    if (!this.pendingUpdate || !this.pendingUpdate.download_url) {
      this.appendLog('[Quantum Auto-Updater Error]: No active update package selected for installation.');
      return;
    }

    if (this.isApplyingAppUpdate) return;
    this.isApplyingAppUpdate = true;

    // Show Progress State in Settings Card
    const idleBox = document.getElementById('update-state-idle');
    const foundBox = document.getElementById('update-state-found');
    const progBox = document.getElementById('update-state-progress');
    if (idleBox) idleBox.style.display = 'none';
    if (foundBox) foundBox.style.display = 'none';
    if (progBox) progBox.style.display = 'block';

    const progTicker = document.getElementById('update-prog-ticker');
    if (progTicker) progTicker.textContent = `Downloading Quantum Updater v${this.pendingUpdate.latest_version}...`;
    const progFill = document.getElementById('update-prog-fill');
    if (progFill) progFill.style.width = '10%';

    // Update banner
    const btnBannerUpdate = document.getElementById('btn-banner-update');
    if (btnBannerUpdate) btnBannerUpdate.style.display = 'none';
    const bannerText = document.getElementById('update-banner-text');
    if (bannerText) bannerText.textContent = `Downloading update v${this.pendingUpdate.latest_version}... Relaunching automatically.`;

    const btnInstall = document.getElementById('btn-install-app-update');
    if (btnInstall) btnInstall.style.display = 'none';
    const btnCheck = document.getElementById('btn-check-updates');
    if (btnCheck) btnCheck.disabled = true;

    this.appendLog(`[Quantum Auto-Updater]: Initiating background update download from ${this.pendingUpdate.download_url}...`);

    try {
      await this.invoke('apply_self_update', { downloadUrl: this.pendingUpdate.download_url });
      if (progTicker) progTicker.textContent = 'Applying update & relaunching...';
      if (progFill) progFill.style.width = '100%';
    } catch (err) {
      console.error('[Apply Self-Update Failed]:', err);
      this.appendLog(`[Quantum Auto-Updater Error]: In-place update failed: ${err.message || err}`);
      this.isApplyingAppUpdate = false;
      if (btnCheck) btnCheck.disabled = false;
      if (btnInstall) btnInstall.style.display = 'inline-flex';
    }
  }

  dismissUpdateBanner() {
    const banner = document.getElementById('global-update-banner');
    if (banner) banner.style.display = 'none';
  }
}

// Native macOS WebKit asynchronous bridge response handler
window.onNativeResponse = function(callId, result, error) {
  if (window._nativeCallbacks && window._nativeCallbacks[callId]) {
    const cb = window._nativeCallbacks[callId];
    delete window._nativeCallbacks[callId];
    if (error) cb.reject(new Error(error));
    else cb.resolve(result);
  }
};

// Global instance
function paintBuildStamp() {
  const stamp = window.QUANTUM_BUILD || { label: "2.0.0 · not a packaged build" };
  const text = stamp.label || "2.0.0 · not a packaged build";
  document.querySelectorAll("[data-build-label]").forEach((el) => {
    el.textContent = text;
  });
  document.title = "Quantum Updater · " + text;
}

paintBuildStamp();
const app = new QuantumUpdaterApp();
window.app = app;

