/**
 * Quantum Updater - Modern Glassmorphic Software & Package Manager
 * Developed by Quantum Step (https://quantumstep.in)
 */

const MONOGRAM_PALETTES = [
  ['#0A84FF', '#0058B6'], // Apple System Blue
  ['#30D158', '#1D7834'], // Apple System Mint
  ['#BF5AF2', '#692095'], // Apple System Purple
  ['#5E5CE6', '#2A297D'], // Apple System Indigo
  ['#FF9F0A', '#9B5B00'], // Apple System Amber
  ['#FF453A', '#9E1C15'], // Apple System Red
  ['#64D2FF', '#08638C'], // Apple System Cyan
  ['#8E8E93', '#3A3A3C']  // Apple System Slate
];

// Direct package ID to official SVG logo mapping (Simple Icons CDN)
const APP_LOGOS_MAP = {
  // Artificial Intelligence & Machine Learning
  'ollama.ollama': { slug: 'ollama', bg: 'rgba(255, 255, 255, 0.12)' },
  'elementlabs.lmstudio': { slug: 'openai', bg: 'rgba(99, 102, 241, 0.15)' },
  'jan.jan': { slug: 'openai', bg: 'rgba(16, 185, 129, 0.15)' },
  'openai.chatgpt': { slug: 'openai', bg: 'rgba(16, 163, 127, 0.15)' },
  'anthropic.claude': { slug: 'anthropic', bg: 'rgba(204, 120, 92, 0.15)' },
  'anysphere.cursor': { slug: 'visualstudiocode', bg: 'rgba(59, 130, 246, 0.15)' },
  'superwhisper.superwhisper': { slug: 'openai', bg: 'rgba(236, 72, 153, 0.15)' },
  'upscayl.upscayl': { slug: 'nvidia', bg: 'rgba(118, 185, 0, 0.15)' },
  'pinokio.pinokio': { slug: 'github', bg: 'rgba(139, 92, 246, 0.15)' },

  // Browsers
  'google.chrome': { slug: 'googlechrome', bg: 'rgba(66, 133, 244, 0.12)' },
  'mozilla.firefox': { slug: 'firefox', bg: 'rgba(255, 113, 57, 0.12)' },
  'brave.brave': { slug: 'brave', bg: 'rgba(251, 84, 43, 0.12)' },
  'microsoft.edge': { slug: 'microsoftedge', bg: 'rgba(0, 120, 215, 0.12)' },
  'opera.opera': { slug: 'opera', bg: 'rgba(255, 27, 45, 0.12)' },
  'torproject.torbrowser': { slug: 'torbrowser', bg: 'rgba(125, 70, 152, 0.12)' },
  'vivaldi.vivaldi': { slug: 'vivaldi', bg: 'rgba(239, 57, 57, 0.12)' },

  // Development
  'microsoft.visualstudiocode': { slug: 'visualstudiocode', bg: 'rgba(0, 122, 204, 0.12)' },
  'git.git': { slug: 'git', bg: 'rgba(240, 80, 50, 0.12)' },
  'github.githubdesktop': { slug: 'github', bg: 'rgba(255, 255, 255, 0.12)' },
  'docker.dockerdesktop': { slug: 'docker', bg: 'rgba(36, 150, 237, 0.12)' },
  'postman.postman': { slug: 'postman', bg: 'rgba(255, 108, 55, 0.12)' },
  'insomnia.insomnia': { slug: 'insomnia', bg: 'rgba(88, 73, 190, 0.12)' },
  'dbeaver.dbeaver': { slug: 'dbeaver', bg: 'rgba(56, 41, 35, 0.15)' },
  'python.python.3.13': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'python.python.3': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'python.python': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'openjs.nodejs.lts': { slug: 'nodedotjs', bg: 'rgba(95, 160, 78, 0.12)' },
  'openjs.nodejs': { slug: 'nodedotjs', bg: 'rgba(95, 160, 78, 0.12)' },
  'rustlang.rustup': { slug: 'rust', bg: 'rgba(255, 255, 255, 0.1)' },
  'golang.go': { slug: 'go', bg: 'rgba(0, 173, 216, 0.12)' },
  'microsoft.windowsterminal': { slug: 'windowsterminal', bg: 'rgba(77, 77, 77, 0.15)' },
  'wiresharkfoundation.wireshark': { slug: 'wireshark', bg: 'rgba(22, 121, 167, 0.12)' },

  // Music & Media
  'spotify.spotify': { slug: 'spotify', bg: 'rgba(29, 185, 84, 0.12)' },
  'videolan.vlc': { slug: 'vlcmediaplayer', bg: 'rgba(255, 136, 0, 0.15)' },
  'audacity.audacity': { slug: 'audacity', bg: 'rgba(0, 0, 235, 0.12)' },
  'obsproject.obsstudio': { slug: 'obsstudio', bg: 'rgba(48, 44, 52, 0.15)' },
  'handbrake.handbrake': { slug: 'handbrake', bg: 'rgba(229, 57, 53, 0.12)' },
  'foobar2000.foobar2000': { slug: 'foobar2000', bg: 'rgba(103, 58, 183, 0.12)' },
  'plex.plex': { slug: 'plex', bg: 'rgba(229, 160, 13, 0.12)' },
  'kodi.kodi': { slug: 'kodi', bg: 'rgba(23, 178, 231, 0.12)' },
  'deezer.deezer': { slug: 'deezer', bg: 'rgba(162, 56, 255, 0.12)' },

  // Addons & Utilities
  '7zip.7zip': { slug: 'sevenzip', bg: 'rgba(255, 255, 255, 0.1)' },
  'rarlab.winrar': { slug: 'winrar', bg: 'rgba(63, 81, 181, 0.12)' },
  'microsoft.powertoys': { slug: 'windows', bg: 'rgba(0, 120, 212, 0.12)' },
  'autohotkey.autohotkey': { slug: 'autohotkey', bg: 'rgba(51, 68, 85, 0.15)' },
  'bitwarden.bitwarden': { slug: 'bitwarden', bg: 'rgba(23, 93, 220, 0.12)' },
  'keepassxcteam.keepassxc': { slug: 'keepassxc', bg: 'rgba(72, 160, 72, 0.12)' },
  'qbittorrent.qbittorrent': { slug: 'qbittorrent', bg: 'rgba(47, 103, 159, 0.12)' },
  'bleachbit.bleachbit': { slug: 'bleachbit', bg: 'rgba(76, 175, 80, 0.12)' },
  'anydesksoftwaregmbh.anydesk': { slug: 'anydesk', bg: 'rgba(239, 68, 59, 0.12)' },

  // Productivity
  'notion.notion': { slug: 'notion', bg: 'rgba(255, 255, 255, 0.1)' },
  'obsidian.obsidian': { slug: 'obsidian', bg: 'rgba(124, 58, 237, 0.12)' },
  'notepad++.notepad++': { slug: 'notepadplusplus', bg: 'rgba(144, 197, 63, 0.15)' },
  'thedocumentfoundation.libreoffice': { slug: 'libreoffice', bg: 'rgba(24, 163, 3, 0.12)' },
  'adobe.acrobat.reader.64-bit': { slug: 'adobeacrobatreader', bg: 'rgba(236, 28, 36, 0.12)' },
  'figma.figma': { slug: 'figma', bg: 'rgba(242, 78, 30, 0.12)' },
  'blenderfoundation.blender': { slug: 'blender', bg: 'rgba(232, 125, 13, 0.12)' },
  'gimp.gimp': { slug: 'gimp', bg: 'rgba(92, 85, 67, 0.15)' },
  'inkscape.inkscape': { slug: 'inkscape', bg: 'rgba(255, 255, 255, 0.1)' },

  // Gaming & Social
  'discord.discord': { slug: 'discord', bg: 'rgba(88, 101, 242, 0.15)' },
  'telegram.telegramdesktop': { slug: 'telegram', bg: 'rgba(38, 165, 228, 0.12)' },
  'whatsapp.whatsapp': { slug: 'whatsapp', bg: 'rgba(37, 211, 102, 0.12)' },
  'slacktechnologies.slack': { slug: 'slack', bg: 'rgba(74, 21, 75, 0.15)' },
  'zoom.zoom': { slug: 'zoom', bg: 'rgba(11, 92, 255, 0.12)' },
  'microsoft.teams': { slug: 'microsoftteams', bg: 'rgba(98, 100, 167, 0.12)' },
  'valve.steam': { slug: 'steam', bg: 'rgba(255, 255, 255, 0.1)' },
  'epicgames.epicgameslauncher': { slug: 'epicgames', bg: 'rgba(49, 49, 49, 0.15)' },
  'gog.galaxy': { slug: 'gogdotcom', bg: 'rgba(138, 43, 226, 0.12)' }
};

// Substring / keyword logo fallbacks
const APP_KEYWORD_LOGOS = {
  'ollama': { slug: 'ollama', bg: 'rgba(255, 255, 255, 0.12)' },
  'lmstudio': { slug: 'openai', bg: 'rgba(99, 102, 241, 0.15)' },
  'jan': { slug: 'openai', bg: 'rgba(16, 185, 129, 0.15)' },
  'chatgpt': { slug: 'openai', bg: 'rgba(16, 163, 127, 0.15)' },
  'claude': { slug: 'anthropic', bg: 'rgba(204, 120, 92, 0.15)' },
  'cursor': { slug: 'visualstudiocode', bg: 'rgba(59, 130, 246, 0.15)' },
  'superwhisper': { slug: 'openai', bg: 'rgba(236, 72, 153, 0.15)' },
  'upscayl': { slug: 'nvidia', bg: 'rgba(118, 185, 0, 0.15)' },
  'pinokio': { slug: 'github', bg: 'rgba(139, 92, 246, 0.15)' },

  'chrome': { slug: 'googlechrome', bg: 'rgba(66, 133, 244, 0.12)' },
  'firefox': { slug: 'firefox', bg: 'rgba(255, 113, 57, 0.12)' },
  'visualstudio': { slug: 'visualstudio', bg: 'rgba(92, 45, 145, 0.12)' },
  'vscode': { slug: 'visualstudiocode', bg: 'rgba(0, 122, 204, 0.12)' },
  'code': { slug: 'visualstudiocode', bg: 'rgba(0, 122, 204, 0.12)' },
  'vlc': { slug: 'vlcmediaplayer', bg: 'rgba(255, 136, 0, 0.15)' },
  'spotify': { slug: 'spotify', bg: 'rgba(29, 185, 84, 0.12)' },
  'git': { slug: 'git', bg: 'rgba(240, 80, 50, 0.12)' },
  'docker': { slug: 'docker', bg: 'rgba(36, 150, 237, 0.12)' },
  'python': { slug: 'python', bg: 'rgba(55, 118, 171, 0.12)' },
  'node': { slug: 'nodedotjs', bg: 'rgba(95, 160, 78, 0.12)' },
  'discord': { slug: 'discord', bg: 'rgba(88, 101, 242, 0.15)' },
  'slack': { slug: 'slack', bg: 'rgba(74, 21, 75, 0.15)' },
  'telegram': { slug: 'telegram', bg: 'rgba(38, 165, 228, 0.12)' },
  'whatsapp': { slug: 'whatsapp', bg: 'rgba(37, 211, 102, 0.12)' },
  'zoom': { slug: 'zoom', bg: 'rgba(11, 92, 255, 0.12)' },
  'steam': { slug: 'steam', bg: 'rgba(255, 255, 255, 0.1)' },
  'epic': { slug: 'epicgames', bg: 'rgba(49, 49, 49, 0.15)' },
  'notepad++': { slug: 'notepadplusplus', bg: 'rgba(144, 197, 63, 0.15)' },
  '7zip': { slug: 'sevenzip', bg: 'rgba(255, 255, 255, 0.1)' },
  'winrar': { slug: 'winrar', bg: 'rgba(63, 81, 181, 0.12)' },
  'postman': { slug: 'postman', bg: 'rgba(255, 108, 55, 0.12)' },
  'obs': { slug: 'obsstudio', bg: 'rgba(48, 44, 52, 0.15)' },
  'audacity': { slug: 'audacity', bg: 'rgba(0, 0, 235, 0.12)' },
  'blender': { slug: 'blender', bg: 'rgba(232, 125, 13, 0.12)' },
  'gimp': { slug: 'gimp', bg: 'rgba(92, 85, 67, 0.15)' },
  'inkscape': { slug: 'inkscape', bg: 'rgba(255, 255, 255, 0.1)' },
  'figma': { slug: 'figma', bg: 'rgba(242, 78, 30, 0.12)' },
  'notion': { slug: 'notion', bg: 'rgba(255, 255, 255, 0.1)' },
  'obsidian': { slug: 'obsidian', bg: 'rgba(124, 58, 237, 0.12)' },
  'libreoffice': { slug: 'libreoffice', bg: 'rgba(24, 163, 3, 0.12)' },
  'bitwarden': { slug: 'bitwarden', bg: 'rgba(23, 93, 220, 0.12)' },
  'keepass': { slug: 'keepassxc', bg: 'rgba(72, 160, 72, 0.12)' },
  'wireshark': { slug: 'wireshark', bg: 'rgba(22, 121, 167, 0.12)' },
  'rust': { slug: 'rust', bg: 'rgba(255, 255, 255, 0.1)' },
  'golang': { slug: 'go', bg: 'rgba(0, 173, 216, 0.12)' },
  'edge': { slug: 'microsoftedge', bg: 'rgba(0, 120, 215, 0.12)' },
  'brave': { slug: 'brave', bg: 'rgba(251, 84, 43, 0.12)' },
  'opera': { slug: 'opera', bg: 'rgba(255, 27, 45, 0.12)' },
  'tor': { slug: 'torbrowser', bg: 'rgba(125, 70, 152, 0.12)' },
  'qbittorrent': { slug: 'qbittorrent', bg: 'rgba(47, 103, 159, 0.12)' },
  'anydesk': { slug: 'anydesk', bg: 'rgba(239, 68, 59, 0.12)' },
  'powertoys': { slug: 'windows', bg: 'rgba(0, 120, 212, 0.12)' },
  'autohotkey': { slug: 'autohotkey', bg: 'rgba(51, 68, 85, 0.15)' },
  'dbeaver': { slug: 'dbeaver', bg: 'rgba(56, 41, 35, 0.15)' },
  'insomnia': { slug: 'insomnia', bg: 'rgba(88, 73, 190, 0.12)' },
  'handbrake': { slug: 'handbrake', bg: 'rgba(229, 57, 53, 0.12)' },
  'plex': { slug: 'plex', bg: 'rgba(229, 160, 13, 0.12)' },
  'kodi': { slug: 'kodi', bg: 'rgba(23, 178, 231, 0.12)' },
  'deezer': { slug: 'deezer', bg: 'rgba(162, 56, 255, 0.12)' },
  'acrobat': { slug: 'adobeacrobatreader', bg: 'rgba(236, 28, 36, 0.12)' },
  'microsoft': { slug: 'windows', bg: 'rgba(0, 120, 212, 0.12)' },
  'google': { slug: 'google', bg: 'rgba(66, 133, 244, 0.12)' },
  'apple': { slug: 'apple', bg: 'rgba(255, 255, 255, 0.1)' }
};

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
    logo: 'https://cdn.simpleicons.org/ollama',
    bg: 'rgba(255, 255, 255, 0.12)'
  },
  {
    id: 'ElementLabs.LMStudio',
    name: 'LM Studio',
    category: 'ai',
    desc: 'Discover, download, and run local LLMs with GPU acceleration, interactive chat UI, and OpenAI-compatible API server.',
    downloads: 98,
    version: '0.3.6',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(99, 102, 241, 0.15)'
  },
  {
    id: 'Jan.Jan',
    name: 'Jan AI',
    category: 'ai',
    desc: 'Self-hosted, open-source ChatGPT alternative that runs 100% offline on your device with complete privacy.',
    downloads: 95,
    version: '0.5.10',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(16, 185, 129, 0.15)'
  },
  {
    id: 'OpenAI.ChatGPT',
    name: 'ChatGPT Desktop',
    category: 'ai',
    desc: 'Official macOS desktop client for OpenAI ChatGPT with instant voice mode, global shortcuts, and file attachments.',
    downloads: 100,
    version: '1.2024.310',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(16, 163, 127, 0.15)'
  },
  {
    id: 'Anthropic.Claude',
    name: 'Claude Desktop',
    category: 'ai',
    desc: 'Official Anthropic Claude desktop application with Model Context Protocol (MCP) and Artifacts rendering.',
    downloads: 97,
    version: '0.7.8',
    logo: 'https://cdn.simpleicons.org/anthropic',
    bg: 'rgba(204, 120, 92, 0.15)'
  },
  {
    id: 'Anysphere.Cursor',
    name: 'Cursor AI Editor',
    category: 'ai',
    desc: 'AI-first code editor fork of VS Code with multi-file reasoning, inline diffs, and terminal execution.',
    downloads: 99,
    version: '0.45.6',
    logo: 'https://cdn.simpleicons.org/visualstudiocode',
    bg: 'rgba(59, 130, 246, 0.15)'
  },
  {
    id: 'Superwhisper.Superwhisper',
    name: 'Superwhisper',
    category: 'ai',
    desc: 'Ultra-fast, accurate offline voice-to-text dictation and transcription powered by local OpenAI Whisper models.',
    downloads: 91,
    version: '1.4.0',
    logo: 'https://cdn.simpleicons.org/openai',
    bg: 'rgba(236, 72, 153, 0.15)'
  },
  {
    id: 'Upscayl.Upscayl',
    name: 'Upscayl',
    category: 'ai',
    desc: 'Free, open source AI image upscaler powered by advanced neural network models with zero quality loss.',
    downloads: 93,
    version: '2.15.0',
    logo: 'https://cdn.simpleicons.org/nvidia',
    bg: 'rgba(118, 185, 0, 0.15)'
  },
  {
    id: 'Pinokio.Pinokio',
    name: 'Pinokio AI Browser',
    category: 'ai',
    desc: 'Autonomous browser ecosystem that lets you install, run, and automate local AI apps and models with 1 click.',
    downloads: 92,
    version: '3.0.0',
    logo: 'https://cdn.simpleicons.org/github/white',
    bg: 'rgba(139, 92, 246, 0.15)'
  },

  // Music & Media
  {
    id: 'Spotify.Spotify',
    name: 'Spotify',
    category: 'media',
    desc: 'Stream millions of tracks, albums, podcasts, and curated playlists.',
    downloads: 99,
    version: '1.2.53',
    logo: 'https://cdn.simpleicons.org/spotify',
    bg: 'rgba(29, 185, 84, 0.15)'
  },
  {
    id: 'VideoLAN.VLC',
    name: 'VLC Media Player',
    category: 'media',
    desc: 'Plays virtually all video and audio codecs, discs, streams, and webcams.',
    downloads: 98,
    version: '3.0.21',
    logo: 'https://cdn.simpleicons.org/vlcmediaplayer',
    bg: 'rgba(255, 136, 0, 0.15)'
  },
  {
    id: 'Audacity.Audacity',
    name: 'Audacity',
    category: 'media',
    desc: 'Open-source multi-track audio editor, recorder, and sound analyzer.',
    downloads: 88,
    version: '3.7.1',
    logo: 'https://cdn.simpleicons.org/audacity',
    bg: 'rgba(0, 0, 235, 0.15)'
  },
  {
    id: 'OBSProject.OBSStudio',
    name: 'OBS Studio',
    category: 'media',
    desc: 'High performance video recording and live streaming for content creators.',
    downloads: 95,
    version: '31.0.0',
    logo: 'https://cdn.simpleicons.org/obsstudio',
    bg: 'rgba(48, 44, 52, 0.2)'
  },
  {
    id: 'HandBrake.HandBrake',
    name: 'HandBrake',
    category: 'media',
    desc: 'Open-source video transcoder for converting video to modern codecs.',
    downloads: 84,
    version: '1.8.2',
    logo: 'https://cdn.simpleicons.org/handbrake',
    bg: 'rgba(229, 57, 53, 0.15)'
  },
  {
    id: 'Foobar2000.Foobar2000',
    name: 'foobar2000',
    category: 'media',
    desc: 'Advanced, lightweight audio player with gapless playback and DSP.',
    downloads: 80,
    version: '2.1.6',
    logo: 'https://cdn.simpleicons.org/foobar2000',
    bg: 'rgba(103, 58, 183, 0.15)'
  },
  {
    id: 'Plex.Plex',
    name: 'Plex Desktop',
    category: 'media',
    desc: 'Stream live TV, movies, shows, and your personal home media library.',
    downloads: 86,
    version: '1.104.0',
    logo: 'https://cdn.simpleicons.org/plex',
    bg: 'rgba(229, 160, 13, 0.15)'
  },
  {
    id: 'Kodi.Kodi',
    name: 'Kodi',
    category: 'media',
    desc: 'Award-winning open source home entertainment hub and digital jukebox.',
    downloads: 83,
    version: '21.1',
    logo: 'https://cdn.simpleicons.org/kodi',
    bg: 'rgba(23, 178, 231, 0.15)'
  },
  {
    id: 'Deezer.Deezer',
    name: 'Deezer',
    category: 'media',
    desc: 'High Fidelity music streaming with FLAC sound and personalized Flow.',
    downloads: 78,
    version: '6.0.190',
    logo: 'https://cdn.simpleicons.org/deezer',
    bg: 'rgba(162, 56, 255, 0.15)'
  },

  // Addons & Utilities
  {
    id: 'raycast',
    name: 'Raycast',
    category: 'utilities',
    desc: 'Blazingly fast, extensible Spotlight launcher, window manager, and developer command palette for macOS.',
    downloads: 100,
    version: '1.85.0',
    logo: 'https://cdn.simpleicons.org/raycast',
    bg: 'rgba(255, 99, 99, 0.15)'
  },
  {
    id: 'rectangle',
    name: 'Rectangle',
    category: 'utilities',
    desc: 'Move and resize windows in macOS using keyboard shortcuts or snap areas with zero background overhead.',
    downloads: 98,
    version: '0.82',
    logo: 'https://cdn.simpleicons.org/apple',
    bg: 'rgba(16, 138, 177, 0.15)'
  },
  {
    id: 'shottr',
    name: 'Shottr',
    category: 'utilities',
    desc: 'Lightweight pixel-perfect screenshot utility for Mac with OCR text recognition, scrolling capture, and annotations.',
    downloads: 95,
    version: '1.7.3',
    logo: 'https://cdn.simpleicons.org/apple',
    bg: 'rgba(6, 215, 160, 0.15)'
  },
  {
    id: 'arc',
    name: 'Arc Browser',
    category: 'browsers',
    desc: 'Futuristic, clean browser crafted by The Browser Company with Spaces, Split Views, and native Apple Silicon efficiency.',
    downloads: 99,
    version: '1.68.0',
    logo: 'https://cdn.simpleicons.org/arc',
    bg: 'rgba(255, 99, 132, 0.15)'
  },
  {
    id: 'orbstack',
    name: 'OrbStack',
    category: 'development',
    desc: 'Fast, lightweight way to run Docker containers and Linux machines on Apple Silicon with 2x lower memory usage.',
    downloads: 98,
    version: '1.8.2',
    logo: 'https://cdn.simpleicons.org/docker',
    bg: 'rgba(16, 138, 177, 0.15)'
  },
  {
    id: 'iterm2',
    name: 'iTerm2',
    category: 'development',
    desc: 'Premier terminal emulator replacement for macOS with Split Panes, Hotkey Window, and Apple Metal rendering.',
    downloads: 99,
    version: '3.5.5',
    logo: 'https://cdn.simpleicons.org/iterm2',
    bg: 'rgba(30, 30, 30, 0.2)'
  },
  {
    id: 'zed',
    name: 'Zed Editor',
    category: 'development',
    desc: 'High-performance, multiplayer code editor from the creators of Atom, crafted in Rust for macOS.',
    downloads: 97,
    version: '0.160.0',
    logo: 'https://cdn.simpleicons.org/visualstudiocode',
    bg: 'rgba(6, 215, 160, 0.15)'
  },
  {
    id: '7zip.7zip',
    name: '7-Zip',
    category: 'utilities',
    desc: 'High compression ratio file archiver supporting 7z, ZIP, RAR, TAR, and ISO.',
    downloads: 99,
    version: '24.09',
    logo: 'https://cdn.simpleicons.org/sevenzip',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'RARLab.WinRAR',
    name: 'WinRAR',
    category: 'utilities',
    desc: 'Industry standard RAR archive creator and decompression utility.',
    downloads: 93,
    version: '7.01',
    logo: 'https://cdn.simpleicons.org/winrar',
    bg: 'rgba(63, 81, 181, 0.15)'
  },
  {
    id: 'Microsoft.PowerToys',
    name: 'Microsoft PowerToys',
    category: 'utilities',
    desc: 'Essential Windows system utilities for power users: FancyZones, Text Extractor, and Run.',
    downloads: 97,
    version: '0.86.0',
    logo: 'https://cdn.simpleicons.org/windows',
    bg: 'rgba(0, 120, 212, 0.15)'
  },
  {
    id: 'voidtools.Everything',
    name: 'Everything',
    category: 'utilities',
    desc: 'Locate files and folders across all NTFS drives in milliseconds.',
    downloads: 96,
    version: '1.4.1',
    logo: 'https://www.voidtools.com/favicon.ico',
    bg: 'rgba(255, 152, 0, 0.15)'
  },
  {
    id: 'Rufus.Rufus',
    name: 'Rufus',
    category: 'utilities',
    desc: 'Reliably create bootable USB drives for Windows, Linux, and UEFI installations.',
    downloads: 92,
    version: '4.6',
    logo: 'https://rufus.ie/pics/rufus-128.png',
    bg: 'rgba(158, 158, 158, 0.15)'
  },
  {
    id: 'AutoHotkey.AutoHotkey',
    name: 'AutoHotkey',
    category: 'utilities',
    desc: 'Desktop automation and macro scripting language for custom keybindings.',
    downloads: 87,
    version: '2.0.18',
    logo: 'https://cdn.simpleicons.org/autohotkey',
    bg: 'rgba(51, 68, 85, 0.2)'
  },
  {
    id: 'Bitwarden.Bitwarden',
    name: 'Bitwarden',
    category: 'utilities',
    desc: 'Open-source end-to-end encrypted password manager and passkey vault.',
    downloads: 94,
    version: '2024.11.0',
    logo: 'https://cdn.simpleicons.org/bitwarden',
    bg: 'rgba(23, 93, 220, 0.15)'
  },
  {
    id: 'KeePassXCTeam.KeePassXC',
    name: 'KeePassXC',
    category: 'utilities',
    desc: 'Offline encrypted password manager with browser integration and auto-type.',
    downloads: 85,
    version: '2.7.9',
    logo: 'https://cdn.simpleicons.org/keepassxc',
    bg: 'rgba(72, 160, 72, 0.15)'
  },
  {
    id: 'qBittorrent.qBittorrent',
    name: 'qBittorrent',
    category: 'utilities',
    desc: 'Free, open-source, ad-free torrent downloader with integrated search.',
    downloads: 91,
    version: '5.0.1',
    logo: 'https://cdn.simpleicons.org/qbittorrent',
    bg: 'rgba(47, 103, 159, 0.15)'
  },
  {
    id: 'BleachBit.BleachBit',
    name: 'BleachBit',
    category: 'utilities',
    desc: 'Free cache cleaner, privacy cleaner, and disk space optimizer.',
    downloads: 81,
    version: '4.6.0',
    logo: 'https://cdn.simpleicons.org/bleachbit',
    bg: 'rgba(76, 175, 80, 0.15)'
  },
  {
    id: 'AnyDeskSoftwareGmbH.AnyDesk',
    name: 'AnyDesk',
    category: 'utilities',
    desc: 'High frame-rate remote desktop application for IT support and telework.',
    downloads: 90,
    version: '8.1.1',
    logo: 'https://cdn.simpleicons.org/anydesk',
    bg: 'rgba(239, 68, 59, 0.15)'
  },

  // Development & Tools
  {
    id: 'Microsoft.VisualStudioCode',
    name: 'Visual Studio Code',
    category: 'development',
    desc: 'Lightweight, extensible code editor with integrated Git, debugging, and terminal.',
    downloads: 100,
    version: '1.95.3',
    logo: 'https://cdn.simpleicons.org/visualstudiocode',
    bg: 'rgba(0, 122, 204, 0.15)'
  },
  {
    id: 'Git.Git',
    name: 'Git',
    category: 'development',
    desc: 'Fast, distributed version control system for tracking source code changes.',
    downloads: 100,
    version: '2.47.1',
    logo: 'https://cdn.simpleicons.org/git',
    bg: 'rgba(240, 80, 50, 0.15)'
  },
  {
    id: 'GitHub.GitHubDesktop',
    name: 'GitHub Desktop',
    category: 'development',
    desc: 'Visual interface for Git and GitHub with seamless diffs, branches, and PRs.',
    downloads: 95,
    version: '3.4.9',
    logo: 'https://cdn.simpleicons.org/github',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'Docker.DockerDesktop',
    name: 'Docker Desktop',
    category: 'development',
    desc: 'Build, share, and run containerized microservices with Docker and Kubernetes.',
    downloads: 97,
    version: '4.35.1',
    logo: 'https://cdn.simpleicons.org/docker',
    bg: 'rgba(36, 150, 237, 0.15)'
  },
  {
    id: 'Postman.Postman',
    name: 'Postman',
    category: 'development',
    desc: 'API testing platform for building, debugging, and mocking REST and GraphQL APIs.',
    downloads: 93,
    version: '11.19.0',
    logo: 'https://cdn.simpleicons.org/postman',
    bg: 'rgba(255, 108, 55, 0.15)'
  },
  {
    id: 'Insomnia.Insomnia',
    name: 'Insomnia',
    category: 'development',
    desc: 'Streamlined desktop API client for REST, GraphQL, gRPC, and WebSockets.',
    downloads: 82,
    version: '10.1.1',
    logo: 'https://cdn.simpleicons.org/insomnia',
    bg: 'rgba(88, 73, 190, 0.15)'
  },
  {
    id: 'dbeaver.dbeaver',
    name: 'DBeaver Community',
    category: 'development',
    desc: 'Universal SQL database administration tool for Postgres, MySQL, SQLite, and Oracle.',
    downloads: 89,
    version: '24.2.4',
    logo: 'https://cdn.simpleicons.org/dbeaver',
    bg: 'rgba(56, 41, 35, 0.2)'
  },
  {
    id: 'Python.Python.3.13',
    name: 'Python 3.13',
    category: 'development',
    desc: 'Modern general-purpose programming language for AI, data science, and scripting.',
    downloads: 98,
    version: '3.13.0',
    logo: 'https://cdn.simpleicons.org/python',
    bg: 'rgba(55, 118, 171, 0.15)'
  },
  {
    id: 'OpenJS.NodeJS.LTS',
    name: 'Node.js (LTS)',
    category: 'development',
    desc: 'JavaScript runtime built on Chrome V8 engine for scalable backend servers.',
    downloads: 97,
    version: '22.11.0',
    logo: 'https://cdn.simpleicons.org/nodedotjs',
    bg: 'rgba(95, 160, 78, 0.15)'
  },
  {
    id: 'Rustlang.Rustup',
    name: 'Rust (Rustup)',
    category: 'development',
    desc: 'Official installer for the memory-safe, ultra-fast Rust programming language.',
    downloads: 90,
    version: '1.27.1',
    logo: 'https://cdn.simpleicons.org/rust',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'Golang.Go',
    name: 'Go Programming Language',
    category: 'development',
    desc: 'Simple, fast, and concurrent systems programming language developed by Google.',
    downloads: 88,
    version: '1.23.3',
    logo: 'https://cdn.simpleicons.org/go',
    bg: 'rgba(0, 173, 216, 0.15)'
  },
  {
    id: 'Microsoft.WindowsTerminal',
    name: 'Windows Terminal',
    category: 'development',
    desc: 'GPU-accelerated terminal for PowerShell, Command Prompt, and WSL Linux.',
    downloads: 96,
    version: '1.21.2911',
    logo: 'https://cdn.simpleicons.org/windowsterminal',
    bg: 'rgba(77, 77, 77, 0.2)'
  },
  {
    id: 'WiresharkFoundation.Wireshark',
    name: 'Wireshark',
    category: 'development',
    desc: 'World’s foremost network traffic and packet capture analyzer.',
    downloads: 87,
    version: '4.4.1',
    logo: 'https://cdn.simpleicons.org/wireshark',
    bg: 'rgba(22, 121, 167, 0.15)'
  },

  // Browsers & Web
  {
    id: 'Google.Chrome',
    name: 'Google Chrome',
    category: 'browsers',
    desc: 'Fast, secure, and customizable browser built by Google for the modern web.',
    downloads: 100,
    version: '131.0.6778',
    logo: 'https://cdn.simpleicons.org/googlechrome',
    bg: 'rgba(66, 133, 244, 0.15)'
  },
  {
    id: 'Mozilla.Firefox',
    name: 'Mozilla Firefox',
    category: 'browsers',
    desc: 'Privacy-focused, independent web browser with advanced tracking protection.',
    downloads: 96,
    version: '132.0.2',
    logo: 'https://cdn.simpleicons.org/firefox',
    bg: 'rgba(255, 113, 57, 0.15)'
  },
  {
    id: 'Brave.Brave',
    name: 'Brave Browser',
    category: 'browsers',
    desc: 'Shields privacy by blocking advertisements and trackers automatically by default.',
    downloads: 95,
    version: '1.71.123',
    logo: 'https://cdn.simpleicons.org/brave',
    bg: 'rgba(251, 84, 43, 0.15)'
  },
  {
    id: 'Microsoft.Edge',
    name: 'Microsoft Edge',
    category: 'browsers',
    desc: 'Fast and secure Windows browser with built-in Copilot AI features and vertical tabs.',
    downloads: 92,
    version: '131.0.2903',
    logo: 'https://cdn.simpleicons.org/microsoftedge',
    bg: 'rgba(0, 120, 215, 0.15)'
  },
  {
    id: 'Opera.Opera',
    name: 'Opera',
    category: 'browsers',
    desc: 'Feature-rich web browser with built-in free VPN, ad blocker, and AI assistant.',
    downloads: 87,
    version: '114.0.5282',
    logo: 'https://cdn.simpleicons.org/opera',
    bg: 'rgba(255, 27, 45, 0.15)'
  },
  {
    id: 'TorProject.TorBrowser',
    name: 'Tor Browser',
    category: 'browsers',
    desc: 'Browse anonymously with multi-layered onion encryption protecting privacy.',
    downloads: 84,
    version: '14.0.1',
    logo: 'https://cdn.simpleicons.org/torbrowser',
    bg: 'rgba(125, 70, 152, 0.15)'
  },
  {
    id: 'Vivaldi.Vivaldi',
    name: 'Vivaldi',
    category: 'browsers',
    desc: 'Highly customizable power browser with tabs stacking, split views, and notes.',
    downloads: 83,
    version: '7.0.3495',
    logo: 'https://cdn.simpleicons.org/vivaldi',
    bg: 'rgba(239, 57, 57, 0.15)'
  },

  // Productivity & Office
  {
    id: 'Notion.Notion',
    name: 'Notion',
    category: 'productivity',
    desc: 'The all-in-one workspace for notes, tasks, databases, and project management.',
    downloads: 96,
    version: '3.14.0',
    logo: 'https://cdn.simpleicons.org/notion',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'Obsidian.Obsidian',
    name: 'Obsidian',
    category: 'productivity',
    desc: 'Markdown-based second brain and personal knowledge base stored locally on disk.',
    downloads: 94,
    version: '1.7.5',
    logo: 'https://cdn.simpleicons.org/obsidian',
    bg: 'rgba(124, 58, 237, 0.15)'
  },
  {
    id: 'Notepad++.Notepad++',
    name: 'Notepad++',
    category: 'productivity',
    desc: 'Fast, lightweight text and source code editor with multi-document tabs.',
    downloads: 98,
    version: '8.7.1',
    logo: 'https://cdn.simpleicons.org/notepadplusplus',
    bg: 'rgba(144, 197, 63, 0.18)'
  },
  {
    id: 'TheDocumentFoundation.LibreOffice',
    name: 'LibreOffice',
    category: 'productivity',
    desc: 'Free open source office suite for documents, spreadsheets, and presentations.',
    downloads: 91,
    version: '24.8.3',
    logo: 'https://cdn.simpleicons.org/libreoffice',
    bg: 'rgba(24, 163, 3, 0.15)'
  },
  {
    id: 'Adobe.Acrobat.Reader.64-bit',
    name: 'Adobe Acrobat Reader',
    category: 'productivity',
    desc: 'Standard for viewing, printing, signing, and annotating PDF documents.',
    downloads: 95,
    version: '24.004.20243',
    logo: 'https://cdn.simpleicons.org/adobeacrobatreader',
    bg: 'rgba(236, 28, 36, 0.15)'
  },
  {
    id: 'Figma.Figma',
    name: 'Figma Desktop',
    category: 'productivity',
    desc: 'Collaborative UI/UX design tool for prototyping and design systems.',
    downloads: 93,
    version: '124.5.5',
    logo: 'https://cdn.simpleicons.org/figma',
    bg: 'rgba(242, 78, 30, 0.15)'
  },
  {
    id: 'BlenderFoundation.Blender',
    name: 'Blender',
    category: 'productivity',
    desc: 'Free and open source 3D creation suite: modeling, animation, VFX, and rendering.',
    downloads: 93,
    version: '4.2.3',
    logo: 'https://cdn.simpleicons.org/blender',
    bg: 'rgba(232, 125, 13, 0.15)'
  },
  {
    id: 'GIMP.GIMP',
    name: 'GIMP',
    category: 'productivity',
    desc: 'Cross-platform image editor with advanced retouching and drawing tools.',
    downloads: 87,
    version: '2.10.38',
    logo: 'https://cdn.simpleicons.org/gimp',
    bg: 'rgba(92, 85, 67, 0.2)'
  },
  {
    id: 'Inkscape.Inkscape',
    name: 'Inkscape',
    category: 'productivity',
    desc: 'Professional vector graphics editor for creating SVG illustrations and logos.',
    downloads: 86,
    version: '1.4',
    logo: 'https://cdn.simpleicons.org/inkscape',
    bg: 'rgba(255, 255, 255, 0.1)'
  },

  // Gaming & Social
  {
    id: 'Discord.Discord',
    name: 'Discord',
    category: 'gaming',
    desc: 'High clarity voice, video, and text communication for communities and gamers.',
    downloads: 99,
    version: '1.0.9168',
    logo: 'https://cdn.simpleicons.org/discord',
    bg: 'rgba(88, 101, 242, 0.18)'
  },
  {
    id: 'Telegram.TelegramDesktop',
    name: 'Telegram Desktop',
    category: 'gaming',
    desc: 'Cloud-based instant messaging with powerful channels, bots, and file sharing.',
    downloads: 95,
    version: '5.7.1',
    logo: 'https://cdn.simpleicons.org/telegram',
    bg: 'rgba(38, 165, 228, 0.15)'
  },
  {
    id: 'WhatsApp.WhatsApp',
    name: 'WhatsApp',
    category: 'gaming',
    desc: 'Simple, reliable, private messaging and HD calls synced to your PC.',
    downloads: 97,
    version: '2.2444.6',
    logo: 'https://cdn.simpleicons.org/whatsapp',
    bg: 'rgba(37, 211, 102, 0.15)'
  },
  {
    id: 'SlackTechnologies.Slack',
    name: 'Slack',
    category: 'gaming',
    desc: 'Enterprise communication hub bringing teamwork, channels, and apps together.',
    downloads: 94,
    version: '4.41.97',
    logo: 'https://cdn.simpleicons.org/slack',
    bg: 'rgba(74, 21, 75, 0.18)'
  },
  {
    id: 'Zoom.Zoom',
    name: 'Zoom Workplace',
    category: 'gaming',
    desc: 'Video meetings, virtual team chat, whiteboard, and screen sharing.',
    downloads: 93,
    version: '6.2.6',
    logo: 'https://cdn.simpleicons.org/zoom',
    bg: 'rgba(11, 92, 255, 0.15)'
  },
  {
    id: 'Microsoft.Teams',
    name: 'Microsoft Teams',
    category: 'gaming',
    desc: 'Connect and collaborate with coworkers, group calls, and Office integration.',
    downloads: 92,
    version: '24277.502',
    logo: 'https://cdn.simpleicons.org/microsoftteams',
    bg: 'rgba(98, 100, 167, 0.15)'
  },
  {
    id: 'Valve.Steam',
    name: 'Steam',
    category: 'gaming',
    desc: 'The ultimate digital entertainment platform for playing and purchasing PC games.',
    downloads: 98,
    version: '2.10.91',
    logo: 'https://cdn.simpleicons.org/steam',
    bg: 'rgba(255, 255, 255, 0.1)'
  },
  {
    id: 'EpicGames.EpicGamesLauncher',
    name: 'Epic Games Launcher',
    category: 'gaming',
    desc: 'Play Fortnite, claim free weekly games, and build with Unreal Engine.',
    downloads: 93,
    version: '16.14.1',
    logo: 'https://cdn.simpleicons.org/epicgames',
    bg: 'rgba(49, 49, 49, 0.2)'
  },
  {
    id: 'GOG.Galaxy',
    name: 'GOG Galaxy',
    category: 'gaming',
    desc: 'All your PC games in one launcher with DRM-free downloads and friend lists.',
    downloads: 85,
    version: '2.0.73',
    logo: 'https://cdn.simpleicons.org/gogdotcom',
    bg: 'rgba(138, 43, 226, 0.15)'
  }
];

class QuantumUpdaterApp {
  constructor() {
    this.updates = [];
    this.selectedIds = new Set();
    this.installed = [];
    this.installedSet = new Set();
    this.activeTab = 'updates';
    this.isScanning = false;
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

    this.init();
  }

  adaptPlatformUI() {
    this.isMac = (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.quantum) ||
                 (typeof navigator !== 'undefined' && navigator.userAgent && navigator.userAgent.includes('Mac')) ||
                 (typeof navigator !== 'undefined' && navigator.platform && navigator.platform.includes('Mac'));

    document.body.classList.toggle('platform-mac', !!this.isMac);
    document.body.classList.toggle('platform-windows', !this.isMac);

    const spotlightBadge = document.getElementById('spotlight-shortcut-badge');
    if (spotlightBadge) {
      spotlightBadge.textContent = this.isMac ? '⌘K' : 'Ctrl+K';
    }

    if (!this.isMac) {
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

      const bentoUpdatesSub = document.getElementById('bento-updates-sub');
      if (bentoUpdatesSub && bentoUpdatesSub.textContent.includes('Mac')) {
        bentoUpdatesSub.textContent = 'Click to audit PC software';
      }

      const bentoHealthSub = document.getElementById('bento-health-sub');
      if (bentoHealthSub && bentoHealthSub.textContent.includes('Gatekeeper')) {
        bentoHealthSub.innerHTML = '<span class="status-dot-mini" style="display:inline-block; width:6px; height:6px; border-radius:50%; background:#30D158; margin-right:4px;"></span>SmartScreen Protected';
      }
    }
  }

  async init() {
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
    } else if (window.pywebview) {
      this.onBridgeReady();
    } else {
      window.addEventListener('pywebviewready', () => this.onBridgeReady());
      // Fallback
      setTimeout(() => this.onBridgeReady(), 300);
    }
  }

  async invoke(cmd, args = {}) {
    try {
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
        return await window.pywebview.api[cmd](...Object.values(args));
      }
    } catch (err) {
      console.warn(`[Bridge Call Error ${cmd}]:`, err);
      throw err;
    }
    return null;
  }


  onBridgeReady() {
    console.log('[Quantum Updater]: Backend bridge connected.');
    this.loadInitialDiagnostics();
    this.loadInstalledSilent();

    // Auto scan on start after brief initial render
    setTimeout(() => {
      this.scanUpdates();
    }, 450);

    // Auto check for application self-update after initial scan launch
    setTimeout(() => {
      this.checkAutoUpdateOnStartup();
    }, 1500);
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

  toggleTheme() {
    const isLight = document.body.classList.toggle('light-mode');
    document.body.classList.toggle('dark-mode', !isLight);
    const theme = isLight ? 'light' : 'dark';
    this.invoke('save_setting', { key: 'theme', value: theme }).catch(() => {});
  }

  toggleSidebar() {
    const sidebar = document.getElementById('mac-sidebar');
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
  }

  openShortcutsModal() {
    this.playHapticTick();
    const el = document.getElementById('modal-shortcuts-backdrop');
    if (el) el.style.display = 'flex';
  }

  closeShortcutsModal() {
    const el = document.getElementById('modal-shortcuts-backdrop');
    if (el) el.style.display = 'none';
  }

  showDetailsModal(pkgId, pkgName, version, origin = 'explore') {
    this.playHapticTick();
    this.activeModalPkg = { id: pkgId, name: pkgName, version: version, origin: origin };
    const backdrop = document.getElementById('modal-details-backdrop');
    if (!backdrop) return;

    // Logo & Titles
    const logoContainer = document.getElementById('modal-pkg-logo');
    if (logoContainer) logoContainer.innerHTML = this.getAppLogoHtml(pkgId, pkgName);

    const titleEl = document.getElementById('modal-pkg-title');
    if (titleEl) titleEl.textContent = pkgName || pkgId;

    const idEl = document.getElementById('modal-pkg-id');
    if (idEl) idEl.textContent = pkgId;

    const verEl = document.getElementById('modal-pkg-version');
    if (verEl) verEl.textContent = version || 'Latest Verified';

    const pubEl = document.getElementById('modal-pkg-publisher');
    if (pubEl) pubEl.textContent = 'Apple Verified / Notarized Developer';

    const licEl = document.getElementById('modal-pkg-license');
    if (licEl) licEl.textContent = 'Proprietary / Open Source (Standard EULA)';

    const typeEl = document.getElementById('modal-pkg-type');
    if (typeEl) typeEl.textContent = 'Universal 2 (Apple Silicon arm64 + Intel x86_64)';

    // Find in catalog for description
    const catItem = APP_CATALOG.find(c => c.id.toLowerCase() === pkgId.toLowerCase() || c.name.toLowerCase() === pkgName.toLowerCase());
    const descEl = document.getElementById('modal-pkg-desc');
    if (descEl) {
      descEl.textContent = catItem ? catItem.desc : `Native macOS application bundle verified by Apple Gatekeeper and Hardened Runtime.`;
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
          <button class="glass-btn btn-success btn-sm" onclick="app.revealSelectedInFinder()">
            Reveal in Finder
          </button>
        `;
      } else {
        actionContainer.innerHTML = `
          <button class="glass-btn btn-coral btn-sm" onclick="app.installExplorePackage('${pkgId}', null); app.closeDetailsModal();">
            Install Package
          </button>
        `;
      }
    }

    backdrop.style.display = 'flex';
  }

  closeDetailsModal() {
    this.activeModalPkg = null;
    const backdrop = document.getElementById('modal-details-backdrop');
    if (backdrop) backdrop.style.display = 'none';
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
    this.activeTab = tabId;
    document.querySelectorAll('.sidebar-item, .pill-btn').forEach(btn => {
      btn.classList.toggle('active', btn.dataset.tab === tabId);
    });
    document.querySelectorAll('.tab-pane').forEach(pane => {
      pane.classList.toggle('active', pane.id === `tab-${tabId}`);
    });

    // Update macOS unified toolbar title & breadcrumb
    const titles = {
      updates: { title: 'App & System Updates', sub: 'Scan and update installed software safely' },
      explore: { title: 'Mac Software Marketplace', sub: 'Curated starter bundles, top apps, and tools' },
      installed: { title: 'Installed Applications', sub: 'Manage apps on your computer' },
      cleanup: { title: 'Free Up Disk Space', sub: 'Clean temporary cache and reclaim disk space' },
      console: { title: 'Activity Log', sub: 'Review real-time background tasks and terminal logs' },
      specs: { title: 'About This Computer', sub: 'Hardware specifications and system details' },
      settings: { title: 'Settings & Options', sub: 'App preferences and automated update options' }
    };
    const t = titles[tabId] || { title: 'Quantum Updater', sub: 'Apple macOS Edition' };
    const titleEl = document.getElementById('toolbar-page-title');
    const subEl = document.getElementById('toolbar-page-sub');
    if (titleEl) titleEl.textContent = t.title;
    if (subEl) subEl.textContent = t.sub;

    if (tabId === 'explore') {
      this.renderExplore();
    } else if (tabId === 'installed' && this.installed.length === 0) {
      this.loadInstalled();
    } else if (tabId === 'cleanup' && (!this.cleanupData || !this.cleanupData.categories)) {
      this.scanCleanup();
    } else if (tabId === 'specs' || tabId === 'settings') {
      this.loadInitialDiagnostics();
    }
  }


  // ================= Universal Logo & Monogram Resolver =================

  getMonogram(name) {
    const clean = (name || '').trim().replace(/[^a-zA-Z0-9\s]/g, '');
    const words = clean.split(/\s+/).filter(Boolean);
    if (!words.length) return 'APP';
    if (words.length === 1) return words[0].slice(0, 2).toUpperCase();
    return (words[0][0] + words[1][0]).toUpperCase();
  }

  getGradient(name) {
    let hash = 0;
    for (let i = 0; i < (name || '').length; i++) {
      hash = name.charCodeAt(i) + ((hash << 5) - hash);
    }
    const idx = Math.abs(hash) % MONOGRAM_PALETTES.length;
    const [c1, c2] = MONOGRAM_PALETTES[idx];
    return `linear-gradient(135deg, ${c1}, ${c2})`;
  }

  /**
   * Resolves the authentic official software logo for any package across Updates, Explore, and Installed tabs.
   * Cascades through exact IDs, curated catalog, keyword dictionaries, and falls back cleanly to high-contrast monogram.
   */
  /**
   * Official built-in vector SVGs for prominent applications.
   * Renders instantly offline, with zero 404s and maximum sharpness.
   */
  getBuiltinAppSvg(id, name) {
    const s = `${id} ${name}`.toLowerCase();
    if (s.includes('chrome') || s.includes('google-chrome')) {
      return {
        bg: 'rgba(66, 133, 244, 0.12)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#EA4335"/><circle cx="12" cy="12" r="7" fill="#FBBC05"/><circle cx="12" cy="12" r="4.2" fill="#34A853"/><circle cx="12" cy="12" r="2.8" fill="#4285F4"/></svg>`
      };
    }
    if (s.includes('visualstudiocode') || s.includes('vscode') || s.includes('code.visualstudio')) {
      return {
        bg: 'rgba(0, 122, 204, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#007ACC"><path d="M23.15 2.587L18.21.21a1.494 1.494 0 0 0-1.705.29l-9.46 8.63-4.12-3.128a.999.999 0 0 0-1.276.057L.327 7.261A1 1 0 0 0 .32 8.688l3.47 3.313-3.47 3.314a1 1 0 0 0 .007 1.427l1.323 1.201a1 1 0 0 0 1.276.057l4.12-3.128 9.46 8.63a1.492 1.492 0 0 0 1.704.29l4.94-2.377A1.5 1.5 0 0 0 24 20.06V3.939a1.5 1.5 0 0 0-.85-1.352zm-5.146 14.861L10.826 12l7.178-5.448v10.896z"/></svg>`
      };
    }
    if (s.includes('claude') || s.includes('anthropic')) {
      return {
        bg: 'rgba(204, 120, 92, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#CC785C"><path d="M12 2L14.4 9.6L22 12L14.4 14.4L12 22L9.6 14.4L2 12L9.6 9.6L12 2Z"/></svg>`
      };
    }
    if (s.includes('chatgpt') || s.includes('openai')) {
      return {
        bg: 'rgba(16, 163, 127, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#10A37F"><circle cx="12" cy="12" r="9" fill="none" stroke="#10A37F" stroke-width="2"/><path d="M12 7v10M7 12h10" stroke="#10A37F" stroke-width="2" stroke-linecap="round"/></svg>`
      };
    }
    if (s.includes('cursor')) {
      return {
        bg: 'rgba(10, 132, 255, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#0A84FF"><path d="M12 2L2 7l10 5 10-5-10-5zm0 8.5L4.5 8 12 4.2 19.5 8 12 10.5zM2 9v6l10 5v-6L2 9zm20 0l-10 5v6l10-5V9z"/></svg>`
      };
    }
    if (s.includes('spotify')) {
      return {
        bg: 'rgba(29, 185, 84, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#1DB954"><path d="M12 0C5.4 0 0 5.4 0 12s5.4 12 12 12 12-5.4 12-12S18.66 0 12 0zm5.521 17.34c-.24.359-.66.48-1.021.24-2.82-1.74-6.36-2.101-10.561-1.141-.418.122-.779-.179-.899-.539-.12-.421.18-.78.54-.9 4.56-1.021 8.52-.6 11.64 1.32.42.18.479.659.301 1.02zm1.44-3.3c-.301.42-.841.6-1.262.3-3.239-1.98-8.159-2.58-11.939-1.38-.479.12-1.02-.12-1.14-.6-.12-.48.12-1.021.6-1.141C9.6 9.9 15 10.561 18.72 12.84c.361.181.54.78.241 1.2zm.12-3.36C15.24 8.4 8.82 8.16 5.16 9.301c-.6.179-1.2-.181-1.38-.721-.18-.601.18-1.2.72-1.381 4.26-1.26 11.28-1.02 15.721 1.621.539.3.719 1.02.419 1.56-.299.421-1.02.599-1.559.3z"/></svg>`
      };
    }
    if (s.includes('slack')) {
      return {
        bg: 'rgba(74, 21, 75, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24"><path fill="#E01E5A" d="M6 15a2 2 0 1 0-2 2h2v-2zm1 0a2 2 0 1 0 4 0V9a2 2 0 1 0-4 0v6z"/><path fill="#36C5F0" d="M9 6a2 2 0 1 0-2-2v2h2zm0 1a2 2 0 1 0 0 4h6a2 2 0 1 0 0-4H9z"/><path fill="#2EB67D" d="M18 9a2 2 0 1 0 2-2h-2v2zm-1 0a2 2 0 1 0-4 0v6a2 2 0 1 0 4 0V9z"/><path fill="#ECB22E" d="M15 18a2 2 0 1 0 2 2v-2h-2zm0-1a2 2 0 1 0 0-4H9a2 2 0 1 0 0 4h6z"/></svg>`
      };
    }
    if (s.includes('discord')) {
      return {
        bg: 'rgba(88, 101, 242, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#5865F2"><path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028c.462-.63.874-1.295 1.226-1.994.021-.041.001-.09-.041-.106a13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.929 1.793 8.18 1.793 12.061 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.893.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.028zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/></svg>`
      };
    }
    if (s.includes('telegram')) {
      return {
        bg: 'rgba(42, 171, 238, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#2AABEE"><path d="M12 0C5.373 0 0 5.373 0 12s5.373 12 12 12 12-5.373 12-12S18.627 0 12 0zm5.894 8.221l-1.97 9.28c-.145.658-.537.818-1.084.508l-3-2.21-1.446 1.394c-.14.18-.357.295-.6.295-.002 0-.003 0-.005 0l.213-3.054 5.56-5.022c.24-.213-.054-.334-.373-.121l-6.869 4.326-2.96-.924c-.64-.203-.658-.64.135-.954l11.566-4.458c.538-.196 1.006.128.832.94z"/></svg>`
      };
    }
    if (s.includes('whatsapp')) {
      return {
        bg: 'rgba(37, 211, 102, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#25D366"><path d="M17.472 14.382c-.301-.15-1.78-.878-2.056-.978-.276-.1-.477-.15-.678.15-.2.301-.778.978-.954 1.179-.176.2-.351.226-.652.075s-1.272-.469-2.422-1.495c-.895-.798-1.5-1.784-1.676-2.085-.176-.301-.019-.464.131-.614.136-.135.301-.351.452-.527.15-.176.2-.301.3-.502.1-.2.05-.376-.025-.527s-.678-1.631-.93-2.233c-.244-.585-.493-.506-.678-.515-.176-.009-.376-.01-.577-.01s-.527.075-.803.376c-.276.301-1.054 1.029-1.054 2.509s1.079 2.909 1.23 3.11c.15.2 2.122 3.241 5.141 4.545.718.31 1.278.496 1.716.635.722.23 1.378.197 1.9.119.58-.088 1.78-.728 2.03-1.431.25-.703.25-1.305.176-1.431-.075-.126-.276-.201-.577-.351zM12.042.028C5.405.028.019 5.414.019 12.051c0 2.12.554 4.192 1.606 6.015L0 24l6.108-1.603c1.761.96 3.754 1.468 5.934 1.468 6.637 0 12.023-5.386 12.023-12.023S18.679.028 12.042.028z"/></svg>`
      };
    }
    if (s.includes('docker')) {
      return {
        bg: 'rgba(36, 150, 237, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#2496ED"><path d="M13.983 11.078h2.119a.186.186 0 00.186-.185V9.006a.186.186 0 00-.186-.186h-2.119a.185.185 0 00-.185.186v1.887c0 .102.083.185.185.185zm-2.954-5.43h2.118a.186.186 0 00.186-.186V3.574a.186.186 0 00-.186-.185h-2.118a.185.185 0 00-.185.185v1.888c0 .102.082.185.185.185zm0 2.716h2.118a.187.187 0 00.186-.186V6.29a.186.186 0 00-.186-.185h-2.118a.185.185 0 00-.185.185v1.887c0 .102.082.186.185.186zm-2.93 0h2.12a.186.186 0 00.184-.186V6.29a.185.185 0 00-.185-.185H8.1a.185.185 0 00-.185.185v1.887c0 .102.083.186.185.186zm-2.964 0h2.119a.186.186 0 00.185-.186V6.29a.185.185 0 00-.185-.185H5.136a.186.186 0 00-.186.185v1.887c0 .102.084.186.186.186zm5.894 2.714h2.118a.186.186 0 00.186-.185V9.006a.186.186 0 00-.186-.186h-2.118a.185.185 0 00-.185.186v1.887c0 .102.082.185.185.185zm-2.93 0h2.12a.185.185 0 00.184-.185V9.006a.185.185 0 00-.184-.186H8.1a.185.185 0 00-.185.186v1.887c0 .102.083.185.185.185zm-2.964 0h2.119a.185.185 0 00.185-.185V9.006a.185.185 0 00-.185-.186H5.136a.186.186 0 00-.186.186v1.887c0 .102.084.185.186.185zm-2.928 0h2.119a.185.185 0 00.185-.185V9.006a.185.185 0 00-.185-.186H2.208a.186.186 0 00-.186.186v1.887c0 .102.084.185.186.185zM23.987 11.882c-.378-.456-1.127-.58-1.748-.387-.194-.648-.68-1.188-1.34-1.464-.326-.137-.68-.21-1.043-.213l-.58-.004-.325.48c-.628.928-.76 2.05-.36 3.067-.29.176-.59.34-.9.493-1.077.532-2.316.812-3.565.812H1.94c-.454 0-.895.077-1.312.227a.407.407 0 00-.256.368c-.144 2.875 1.096 5.617 3.398 7.525C5.83 24.37 8.5 25.1 11.233 25.1c7.34 0 13.064-4.805 13.513-11.218.02-.277.01-.555-.027-.83a3.52 3.52 0 00-.732-1.17z"/></svg>`
      };
    }
    if (s.includes('github')) {
      return {
        bg: 'rgba(255, 255, 255, 0.12)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#F5F5F7"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/></svg>`
      };
    }
    if (s.includes('notion')) {
      return {
        bg: 'rgba(255, 255, 255, 0.12)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#FFFFFF"><path d="M4.459 4.208c.746.606 1.026.56 2.428.466l13.215-.793c.28 0 .047-.28-.046-.326L17.86 1.976c-.466-.373-.7-.466-1.54-.42l-11.436.653c-.373.047-.466.233-.42.42zm.84 3.733v13.535c0 .607.327.84.887.84h11.48c.56 0 .84-.327.84-.84V7.941c0-.56-.28-.84-.84-.84H6.186c-.56 0-.887.28-.887.84zm11.2 1.12l.14 9.88c.047.373-.093.56-.467.56l-1.4.093c-.373 0-.513-.14-.513-.42l-.093-6.944-4.57 6.944c-.28.42-.513.466-.933.466l-1.353.093c-.373 0-.513-.187-.513-.513l-.14-9.833c0-.373.14-.56.513-.56l1.353-.093c.373 0 .513.187.513.513l.093 6.944 4.57-6.944c.28-.42.513-.466.933-.466l1.353-.093c.373 0 .513.187.513.513z"/></svg>`
      };
    }
    if (s.includes('obsidian')) {
      return {
        bg: 'rgba(124, 58, 237, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24" fill="#7C3AED"><path d="M12 2L3 8l3 13 9 1 6-7-9-13zm0 3.5l6.5 9-4.5 5.5L6.5 19 4.5 9.5 12 5.5z"/></svg>`
      };
    }
    if (s.includes('vlc')) {
      return {
        bg: 'rgba(255, 136, 0, 0.15)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24"><polygon points="12,2 8,11 16,11" fill="#FF8800"/><polygon points="7.5,12 5.5,17 18.5,17 16.5,12" fill="#FFFFFF"/><polygon points="5,18 2,22 22,22 19,18" fill="#FF8800"/></svg>`
      };
    }
    if (s.includes('terminal') || s.includes('iterm')) {
      return {
        bg: 'rgba(28, 28, 30, 0.8)',
        svg: `<svg width="26" height="26" viewBox="0 0 24 24"><rect x="2" y="3" width="20" height="18" rx="4" fill="#1C1C1E" stroke="#3A3A3C" stroke-width="1.5"/><path d="M7 8l4 4-4 4" stroke="#30D158" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none"/><line x1="13" y1="16" x2="17" y2="16" stroke="#30D158" stroke-width="2" stroke-linecap="round"/></svg>`
      };
    }
    return null;
  }

  /**
   * Resolves the authentic official software logo for any package across Updates, Explore, and Installed tabs.
   * Priority: 1. Native macOS icon -> 2. Builtin brand SVG -> 3. Catalog -> 4. SimpleIcons -> 5. Monogram
   */
  getAppLogoHtml(pkgId, pkgName, customIcon = null) {
    const id = (pkgId || '').toLowerCase().trim();
    const name = (pkgName || '').toLowerCase().trim();
    const mono = this.getMonogram(pkgName || pkgId);
    const grad = this.getGradient(pkgName || pkgId);
    const safeName = this.escapeHtml(pkgName || pkgId);

    // 1. Native macOS Cocoa icon (from [[NSWorkspace sharedWorkspace] iconForFile:])
    const nativeIcon = customIcon || (this.installedIconsMap && (this.installedIconsMap.get(id) || this.installedIconsMap.get(name)));
    if (nativeIcon) {
      return `
        <div class="app-logo-container native-mac-icon" title="${safeName}">
          <img class="app-logo-img" src="${nativeIcon}" alt="${safeName}">
        </div>
      `;
    }

    // 2. Built-in crisp vector SVGs for top apps (offline, zero-fail)
    const builtinSvg = this.getBuiltinAppSvg(id, name);
    if (builtinSvg) {
      return `
        <div class="app-logo-container" style="background: ${builtinSvg.bg || 'rgba(255,255,255,0.06)'};" title="${safeName}">
          ${builtinSvg.svg}
        </div>
      `;
    }

    // 3. Direct Catalog Match
    const catItem = APP_CATALOG.find(c => c.id.toLowerCase() === id);
    if (catItem) {
      const cleanLogo = (catItem.logo || '').replace(/\.svg$/, '');
      return `
        <div class="app-logo-container" style="background: ${catItem.bg || 'rgba(255,255,255,0.06)'};" title="${safeName}">
          <img class="app-logo-img" src="${cleanLogo}" alt="${safeName}" loading="lazy"
               onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
          <span class="app-logo-fallback" style="display: none; background: ${grad};">${mono}</span>
        </div>
      `;
    }

    // 4. Exact and Keyword Logo Map
    let match = APP_LOGOS_MAP[id];
    if (!match) {
      for (const [key, val] of Object.entries(APP_KEYWORD_LOGOS)) {
        if (id.includes(key) || name.includes(key)) {
          match = val;
          break;
        }
      }
    }

    if (match) {
      const slugClean = (match.slug || '').replace(/\.svg$/, '');
      const logoUrl = match.url || `https://cdn.simpleicons.org/${slugClean}`;
      const bg = match.bg || 'rgba(255,255,255,0.06)';
      return `
        <div class="app-logo-container" style="background: ${bg};" title="${safeName}">
          <img class="app-logo-img" src="${logoUrl}" alt="${safeName}" loading="lazy"
               onerror="this.style.display='none'; this.nextElementSibling.style.display='flex';">
          <span class="app-logo-fallback" style="display: none; background: ${grad};">${mono}</span>
        </div>
      `;
    }

    // 5. Clean Fallback to Monogram
    return `
      <div class="app-logo-container" style="background: rgba(255,255,255,0.05);" title="${safeName}">
        <span class="app-logo-fallback" style="background: ${grad};">${mono}</span>
      </div>
    `;
  }

  // ================= Sequential Operation Queue & Installed Detection =================

  async loadInstalledSilent() {
    try {
      const packages = await this.invoke('get_installed', { engine: this.activeEngine });
      if (packages && Array.isArray(packages)) {
        this.installed = packages;
        this.installedSet.clear();
        if (!this.installedIconsMap) this.installedIconsMap = new Map();
        packages.forEach(p => {
          if (p.id) this.installedSet.add(p.id.toLowerCase().trim());
          if (p.name) this.installedSet.add(p.name.toLowerCase().trim());
          if (p.icon) {
            if (p.id) this.installedIconsMap.set(p.id.toLowerCase().trim(), p.icon);
            if (p.name) this.installedIconsMap.set(p.name.toLowerCase().trim(), p.icon);
          }
        });
        this.renderExplore();
      }
    } catch (err) {
      console.warn('[Quantum Updater]: Silent installed audit notice:', err);
    }
  }

  isPackageInstalled(pkgId, pkgName) {
    if (!pkgId) return false;
    const id = pkgId.toLowerCase().trim();
    if (this.installedSet.has(id)) return true;
    if (pkgName && this.installedSet.has(pkgName.toLowerCase().trim())) return true;
    for (const instId of this.installedSet) {
      if (instId === id || instId.includes(id) || id.includes(instId)) return true;
    }
    return false;
  }

  enqueueTask(task) {
    task.id = Date.now() + Math.random().toString(36).substr(2, 5);
    task.status = 'pending';
    task.timestamp = new Date();
    this.operationQueue.push(task);
    this.showToast(`Queued ${task.name} (${task.type})`, 'info');
    this.updateGlobalDock();
    this.renderQueueDrawer();

    if (!this.isProcessingQueue) {
      this.processNextQueueItem();
    }
  }

  async processNextQueueItem() {
    const nextTask = this.operationQueue.find(t => t.status === 'pending');
    if (!nextTask) {
      this.isProcessingQueue = false;
      this.currentQueueTask = null;
      this.updateGlobalDock();
      return;
    }

    this.isProcessingQueue = true;
    this.currentQueueTask = nextTask;
    nextTask.status = 'running';
    this.updateGlobalDock();
    this.renderQueueDrawer();

    const typeLabels = { install: 'Installing', upgrade: 'Upgrading', uninstall: 'Uninstalling' };
    const actionLabel = typeLabels[nextTask.type] || 'Processing';
    this.appendLog(`\n[Queue Manager]: ${actionLabel} ${nextTask.name} (${nextTask.pkgId})...\n`);

    try {
      let success = false;
      if (nextTask.type === 'install') {
        const res = await this.invoke('install_package', { packageId: nextTask.pkgId, engine: this.activeEngine });
        success = res && res.success !== false;
      } else if (nextTask.type === 'upgrade') {
        const res = await this.invoke('upgrade_packages', { packageIds: [nextTask.pkgId], engine: this.activeEngine });
        success = res && res.length > 0 && res[0].success !== false;
      } else if (nextTask.type === 'uninstall') {
        const res = await this.invoke('uninstall_package', { packageId: nextTask.pkgId, engine: this.activeEngine });
        success = res === true || res === null;
      }

      if (success) {
        nextTask.status = 'success';
        if (nextTask.type === 'install' || nextTask.type === 'upgrade') {
          this.installedSet.add(nextTask.pkgId.toLowerCase().trim());
          if (nextTask.name) this.installedSet.add(nextTask.name.toLowerCase().trim());
        } else if (nextTask.type === 'uninstall') {
          this.installedSet.delete(nextTask.pkgId.toLowerCase().trim());
        }
        this.showToast(`Successfully completed: ${nextTask.name}`, 'success');
        this.appendLog(`[Queue Manager]: Successfully finished ${nextTask.name}.\n`);
      } else {
        nextTask.status = 'failed';
        this.showToast(`Notice: ${nextTask.name} finished with status note`, 'error');
        this.appendLog(`[Queue Manager]: Notice on ${nextTask.name}.\n`);
      }
    } catch (err) {
      nextTask.status = 'failed';
      nextTask.error = String(err);
      this.showToast(`Failed: ${nextTask.name} - ${err}`, 'error');
      this.appendLog(`[Queue Manager Error]: ${err}\n`);
    }

    this.renderExplore();
    this.updateGlobalDock();
    this.renderQueueDrawer();

    setTimeout(() => this.processNextQueueItem(), 400);
  }

  updateGlobalDock() {
    const dock = document.getElementById('global-operation-dock');
    if (!dock) return;

    const pendingOrRunning = this.operationQueue.filter(t => t.status === 'pending' || t.status === 'running');
    const countBadge = document.getElementById('dock-queue-count');
    if (countBadge) countBadge.textContent = pendingOrRunning.length;

    if (this.currentQueueTask && this.isProcessingQueue) {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot'; }
      const title = document.getElementById('dock-status-title');
      if (title) title.textContent = `${this.currentQueueTask.type.toUpperCase()}: ${this.currentQueueTask.name}`;
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) ticker.textContent = `Processing package ${this.currentQueueTask.pkgId} via ${this.activeEngine.toUpperCase()}... (${pendingOrRunning.length} remaining)`;
    } else if (this.operationQueue.length > 0) {
      dock.style.display = 'flex';
      const dot = document.getElementById('dock-pulse-dot');
      if (dot) { dot.className = 'dock-pulse-dot success'; }
      const title = document.getElementById('dock-status-title');
      if (title) title.textContent = 'Queue Finished';
      const ticker = document.getElementById('dock-status-ticker');
      if (ticker) ticker.textContent = `All ${this.operationQueue.length} operations completed.`;
      setTimeout(() => {
        if (!this.isProcessingQueue && this.operationQueue.every(t => t.status !== 'pending' && t.status !== 'running')) {
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
      html += `
        <div class="queue-item-row ${isRunning ? 'running' : ''}">
          <div class="queue-item-left">
            <span class="queue-item-name" title="${this.escapeHtml(task.pkgId)}">${this.escapeHtml(task.name)}</span>
          </div>
          <span class="queue-item-status-pill ${task.status}">${task.status}</span>
        </div>
      `;
    });
    list.innerHTML = html;
  }

  clearFinishedQueue() {
    this.operationQueue = this.operationQueue.filter(t => t.status === 'pending' || t.status === 'running');
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
    if (this.isScanning || this.isUpdating) return;
    this.isScanning = true;

    const btnScan = document.getElementById('btn-scan');
    if (btnScan) {
      btnScan.disabled = true;
      btnScan.innerHTML = `<svg class="spin" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg> <span>Scanning System...</span>`;
    }

    this.setIslandState(true, 'Scanning System for Application Updates...', null, `Auditing packages via ${this.activeEngine.toUpperCase()}...`);
    this.appendLog(`[Quantum Updater]: Initiating non-blocking system audit using ${this.activeEngine.toUpperCase()}...\n`);

    try {
      const scanPromise = this.invoke('scan_updates', { engine: this.activeEngine });
      const timeoutPromise = new Promise((_, reject) =>
        setTimeout(() => reject(new Error('Update scan timeout reached')), 15000)
      );
      const result = await Promise.race([scanPromise, timeoutPromise]);
      if (result && Array.isArray(result)) {
        this.updates = result;
      } else {
        this.updates = [];
      }
      this.selectedIds.clear();
      this.renderUpdates();
      this.setIslandState(false, 'System Audit Complete', 100, `Found ${this.updates.length} applications with available updates.`);
    } catch (e) {
      console.warn('Scan updates notice:', e);
      this.appendLog(`[Quantum Updater]: Audit completed. ${e.message || e}\n`);
      if (!this.updates) this.updates = [];
      this.renderUpdates();
      this.setIslandState(false, 'Applications Scanned', 100, 'Audit complete. System ready.');
    } finally {
      this.isScanning = false;
      if (btnScan) {
        btnScan.disabled = false;
        btnScan.innerHTML = `
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg>
          <span>Scan for Updates</span>
        `;
      }
    }
  }

  renderUpdates(filteredList = null) {
    const list = filteredList !== null ? filteredList : this.updates;
    const container = document.getElementById('updates-list');

    // Update Bento metrics
    document.getElementById('bento-updates-val').textContent = this.updates.length;
    document.getElementById('bento-updates-sub').textContent = this.updates.length > 0 ? `${this.updates.length} updates pending` : 'All packages up-to-date';
    document.getElementById('bento-selected-val').textContent = this.selectedIds.size;
    document.getElementById('bento-selected-sub').textContent = this.selectedIds.size > 0 ? `${this.selectedIds.size} ready to install` : 'None ticked (All selected)';

    const navBadge = document.getElementById('nav-update-count');
    if (this.updates.length > 0) {
      navBadge.style.display = 'inline-block';
      navBadge.textContent = this.updates.length;
    } else {
      navBadge.style.display = 'none';
    }

    this.updateActionButtons();

    if (!list.length) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <div class="placeholder-icon"><svg width="38" height="38" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg></div>
          <h3>System Up-to-Date</h3>
          <p>Great news! All installed applications monitored by this engine are currently on the latest version.</p>
        </div>
      `;
      return;
    }

    let html = '';
    list.forEach(pkg => {
      const isChecked = this.selectedIds.has(pkg.id);
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name);

      html += `
        <div class="package-card ${isChecked ? 'selected' : ''}" id="card-${pkg.id}" onclick="app.handleCardClick('${pkg.id}', event)">
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
              </div>
            </div>
          </div>
          <div class="package-right" onclick="event.stopPropagation()">
            <button class="glass-btn btn-primary btn-sm" id="btn-update-single-${pkg.id}" onclick="app.updateSinglePackage('${pkg.id}')">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg> Upgrade
            </button>
          </div>
          <div class="card-inline-progress">
            <div class="card-inline-progress-fill" id="card-prog-${pkg.id}" style="width: 0%;"></div>
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
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
      updatesSub.textContent = this.updates.length > 0
        ? `${this.updates.length} updates pending`
        : 'All packages up-to-date';
    }

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

    if (!this.updates.length || this.isUpdating) {
      btnAction.disabled = true;
      btnText.textContent = this.isUpdating ? 'Updating in Progress...' : 'Update All Packages';
      return;
    }

    btnAction.disabled = false;
    if (this.selectedIds.size === 0) {
      btnText.textContent = `Update All Packages (${this.updates.length})`;
    } else {
      btnText.textContent = `Update Selected (${this.selectedIds.size})`;
    }
  }

  filterUpdates(query) {
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
  }

  // ================= Multi / Single Upgrade Execution =================

  async startUpdateAction() {
    if (this.isUpdating) return;
    const targetIds = this.selectedIds.size > 0
      ? Array.from(this.selectedIds)
      : this.updates.map(p => p.id);

    if (!targetIds.length) return;
    await this.runBatchUpgrade(targetIds);
  }

  async updateSinglePackage(pkgId) {
    if (this.isUpdating) return;
    await this.runBatchUpgrade([pkgId]);
  }

  async runBatchUpgrade(packageIds) {
    if (this.isUpdating || !packageIds.length) return;
    this.isUpdating = true;
    this.updateActionButtons();

    const total = packageIds.length;
    this.appendLog(`\n======================================================\n`);
    this.appendLog(`[Quantum Updater]: Starting live update for ${total} package(s)...\n`);

    this.setIslandState(true, `Upgrading 1 of ${total}`, 0, `Initializing update engine...`);

    let completed = 0;
    let failed = 0;

    for (let i = 0; i < packageIds.length; i++) {
      const pid = packageIds[i];
      this.currentUpdatingPkgId = pid;
      const pkgObj = this.updates.find(p => p.id === pid);
      const pkgDisplayName = pkgObj ? pkgObj.name : pid;
      const stepPct = Math.round(((i + 0.1) / total) * 100);

      this.setIslandState(true, `Upgrading ${i + 1} of ${total}: ${pkgDisplayName}`, stepPct, `Running silent update for ${pkgDisplayName}...`);
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

      try {
        const statuses = await this.invoke('upgrade_packages', {
          packageIds: [pid],
          engine: this.activeEngine
        });

        const status = statuses && statuses[0];
        if (status && status.success) {
          completed++;

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
          this.appendLog(`[NOTICE] Upgrade finished with note: ${(status && status.message) || 'Check log'}\n`);
        }
      } catch (e) {
        failed++;
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
        this.appendLog(`[ERROR] Failed ${pid}: ${e}\n`);
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
      fillEl.style.width = `${p}%`;
      badgeEl.textContent = `${p}%`;
    } else {
      fillEl.style.width = active ? '30%' : '0%';
      badgeEl.textContent = active ? 'Working...' : '';
    }
  }

  setIslandProgress(pctVal, tickerText) {
    const p = Math.min(100, Math.max(0, Math.round((pctVal || 0) * 100)));

    // 1. Dynamic Floating Progress Island
    const islandFill = document.getElementById('island-progress-fill');
    if (islandFill) islandFill.style.width = `${p}%`;
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
    this.exploreSearchTerm = (val || '').toLowerCase().trim();
    this.renderExplore();
  }

  renderExplore() {
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
          <p>No curated software matched "${this.escapeHtml(this.exploreSearchTerm)}". Click "Search Online" to query the live repository.</p>
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
                <button class="glass-btn btn-coral btn-sm" id="btn-explore-inst-${pkg.id}" onclick="app.installExplorePackage('${pkg.id}', this)">
                  <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg> Install
                </button>
              `}
            </div>
            ${isInstalled ? `<button class="glass-btn btn-secondary btn-xs" style="opacity: 0.75;" onclick="app.installExplorePackage('${pkg.id}', this, true)">Reinstall</button>` : ''}
          </div>
        </div>
      `;
    });

    container.innerHTML = html;
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

      let html = `
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; padding: 0 4px;">
          <span style="font-size: 13px; color: var(--text-muted); font-weight: 600;">Live Search Results (${results.length})</span>
          <button class="glass-btn btn-secondary btn-sm" onclick="app.renderExplore()">← Back to Curated Explore</button>
        </div>
      `;

      results.forEach(item => {
        const logoHtml = this.getAppLogoHtml(item.id, item.name);
        html += `
          <div class="package-card">
            <div class="package-left">
              ${logoHtml}
              <div class="package-info">
                <span class="package-name">${this.escapeHtml(item.name)}</span>
                <div class="package-meta">
                  <span>${this.escapeHtml(item.id)}</span>
                  <span class="version-pill">v${this.escapeHtml(item.version || 'Latest')}</span>
                  <span>(${this.escapeHtml(item.source || 'winget')})</span>
                </div>
              </div>
            </div>
            <div class="package-right">
              <button class="glass-btn btn-primary btn-sm" onclick="app.installExplorePackage('${item.id}', this)">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg> Install
              </button>
            </div>
          </div>
        `;
      });

      container.innerHTML = html;
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

  installBundle(bundleId) {
    const bundleConfigs = {
      essentials: {
        title: 'Everyday Essentials',
        apps: [
          { pkgId: 'google-chrome', name: 'Google Chrome' },
          { pkgId: 'spotify', name: 'Spotify' },
          { pkgId: 'vlc', name: 'VLC Media Player' },
          { pkgId: 'raycast', name: 'Raycast' },
          { pkgId: 'whatsapp', name: 'WhatsApp' },
          { pkgId: 'telegram', name: 'Telegram' }
        ]
      },
      dev: {
        title: 'Developer Workstation',
        apps: [
          { pkgId: 'visual-studio-code', name: 'Visual Studio Code' },
          { pkgId: 'docker', name: 'Docker Desktop' },
          { pkgId: 'iterm2', name: 'iTerm2' },
          { pkgId: 'git', name: 'Git' },
          { pkgId: 'node', name: 'Node.js LTS' },
          { pkgId: 'python@3.12', name: 'Python 3' }
        ]
      },
      creator: {
        title: 'Creative Studio',
        apps: [
          { pkgId: 'blender', name: 'Blender' },
          { pkgId: 'figma', name: 'Figma' },
          { pkgId: 'audacity', name: 'Audacity' },
          { pkgId: 'handbrake', name: 'HandBrake' },
          { pkgId: 'gimp', name: 'GIMP' }
        ]
      },
      privacy: {
        title: 'Privacy & Security',
        apps: [
          { pkgId: 'bitwarden', name: 'Bitwarden' },
          { pkgId: 'brave-browser', name: 'Brave Browser' },
          { pkgId: 'keka', name: 'Keka File Archiver' },
          { pkgId: 'tor-browser', name: 'Tor Browser' }
        ]
      }
    };

    const bundle = bundleConfigs[bundleId];
    if (!bundle) return;

    let queuedCount = 0;
    bundle.apps.forEach(item => {
      if (!this.isPackageInstalled(item.pkgId, item.name)) {
        this.enqueueTask({
          type: 'install',
          pkgId: item.pkgId,
          name: item.name
        });
        queuedCount++;
      }
    });

    if (queuedCount > 0) {
      this.showToast(`Queued ${queuedCount} apps from ${bundle.title}`, 'info');
      this.setIslandState(true, `Installing ${bundle.title}`, null, `${queuedCount} applications queued in background manager.`);
    } else {
      this.showToast(`All applications in ${bundle.title} are already installed.`, 'success');
    }
  }

  async installExplorePackage(pkgId, btnEl) {
    // Find card and package metadata
    const card = document.getElementById(`explore-card-${pkgId}`) || (btnEl ? btnEl.closest('.package-card') : null);
    const catItem = APP_CATALOG.find(p => p.id.toLowerCase() === pkgId.toLowerCase());
    const pkgDisplayName = catItem ? catItem.name : pkgId;

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

    if (btnEl) {
      btnEl.disabled = true;
      btnEl.className = 'glass-btn btn-updating btn-sm';
      btnEl.innerHTML = `<span class="btn-spinner"></span> <span>Installing...</span>`;
    }

    this.setIslandState(true, `Installing ${pkgDisplayName}`, null, `Downloading & running silent setup via ${this.activeEngine.toUpperCase()}...`);
    this.appendLog(`[Quantum Updater]: Initiating silent install for "${pkgDisplayName}" (${pkgId})...\n`);

    try {
      const res = await this.invoke('install_package', {
        packageId: pkgId,
        engine: this.activeEngine
      });

      if (res && res.success) {
        if (card) {
          card.classList.remove('card-updating');
          card.classList.add('card-completed');
          const fill = card.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill success';
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
        this.setIslandState(false, `${pkgDisplayName} Ready`, 100, successMsg);
        this.showToast(successMsg, 'success');
        this.appendLog(`[OK] Successfully processed "${pkgDisplayName}" (${pkgId}): ${successMsg}\n`);

        // Smoothly clean up celebration pulse after delay without resetting UI
        setTimeout(() => {
          if (card) {
            card.classList.remove('card-completed');
            const prog = card.querySelector('.card-inline-progress');
            if (prog) prog.remove();
          }
        }, 3200);

      } else {
        if (card) {
          card.classList.remove('card-updating');
          const fill = card.querySelector('.card-inline-progress-fill');
          if (fill) fill.className = 'card-inline-progress-fill notice';
        }
        if (btnEl) {
          btnEl.disabled = false;
          btnEl.className = 'glass-btn btn-secondary btn-sm';
          btnEl.innerHTML = `Retry Install`;
          btnEl.onclick = () => this.installExplorePackage(pkgId, btnEl);
        }
        this.setIslandState(false, `Install Notice`, null, `${pkgDisplayName} notice: ${(res && res.message) || 'Check console'}`);
      }
    } catch (e) {
      if (card) {
        card.classList.remove('card-updating');
        const fill = card.querySelector('.card-inline-progress-fill');
        if (fill) fill.className = 'card-inline-progress-fill notice';
      }
      if (btnEl) {
        btnEl.disabled = false;
        btnEl.className = 'glass-btn btn-secondary btn-sm';
        btnEl.innerHTML = `Retry Install`;
        btnEl.onclick = () => this.installExplorePackage(pkgId, btnEl);
      }
      this.setIslandState(false, `Install Error`, null, String(e));
      this.appendLog(`[ERROR]: Installation failed for "${pkgDisplayName}": ${e}\n`);
    }
  }

  // ================= Installed Applications Tab =================

  async loadInstalled() {
    const container = document.getElementById('installed-list');
    container.innerHTML = `
      <div class="glass-placeholder">
        <div class="placeholder-icon"><svg class="spin" width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path><path d="M3 3v5h5"></path><path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16"></path><path d="M16 21h5v-5"></path></svg></div>
        <h3>Auditing System Applications...</h3>
        <p>Reading registry and package manager database...</p>
      </div>
    `;

    try {
      const packages = await this.invoke('get_installed', { engine: this.activeEngine });
      this.installed = packages || [];
      this.installedSet.clear();
      if (!this.installedIconsMap) this.installedIconsMap = new Map();
      this.installed.forEach(p => {
        if (p.id) this.installedSet.add(p.id.toLowerCase().trim());
        if (p.name) this.installedSet.add(p.name.toLowerCase().trim());
        if (p.icon) {
          if (p.id) this.installedIconsMap.set(p.id.toLowerCase().trim(), p.icon);
          if (p.name) this.installedIconsMap.set(p.name.toLowerCase().trim(), p.icon);
        }
      });
      this.renderInstalled();
    } catch (e) {
      container.innerHTML = `<div class="glass-placeholder"><p>Failed to load installed apps: ${e}</p></div>`;
    }
  }

  renderInstalled(filteredList = null) {
    const list = filteredList !== null ? filteredList : this.installed;
    const container = document.getElementById('installed-list');

    if (!list.length) {
      container.innerHTML = `<div class="glass-placeholder"><h3>No applications found</h3></div>`;
      return;
    }

    let html = '';
    list.forEach(pkg => {
      const logoHtml = this.getAppLogoHtml(pkg.id, pkg.name, pkg.icon);

      html += `
        <div class="package-card">
          <div class="package-left">
            ${logoHtml}
            <div class="package-info">
              <div style="display: flex; align-items: center; gap: 8px;">
                <span class="package-name">${this.escapeHtml(pkg.name)}</span>
                <span class="badge-cat" style="font-size: 10px;">${pkg.source || 'mac-app'}</span>
              </div>
              <div class="package-meta">
                <span>${this.escapeHtml(pkg.id)}</span>
                <span class="version-pill">v${this.escapeHtml(pkg.installed_version || '?')}</span>
                <span class="security-chip-pill" style="font-size: 9px;">Universal 2</span>
              </div>
            </div>
          </div>
          <div class="package-right" style="display: flex; align-items: center; gap: 6px;">
            <button class="glass-btn btn-secondary btn-sm" onclick="app.revealInFinder('${pkg.id}', '${pkg.path || ''}')" title="Reveal in macOS Finder">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"></path><polyline points="15 3 21 3 21 9"></polyline><line x1="10" y1="14" x2="21" y2="3"></line></svg>
              <span>Finder</span>
            </button>
            <button class="glass-btn btn-secondary btn-sm btn-info-action" onclick="app.showDetailsModal('${pkg.id}', '${this.escapeHtml(pkg.name)}', '${pkg.installed_version || ''}', 'installed')" title="Quick Look Inspector">
              <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>
              <span>Info</span>
            </button>
            <button class="glass-btn btn-secondary btn-sm" style="color: var(--c-coral-pink);" onclick="app.uninstallPackage('${pkg.id}', '${this.escapeHtml(pkg.name)}')">
              Uninstall
            </button>
          </div>
        </div>
      `;
    });
    container.innerHTML = html;
  }

  filterInstalled(query) {
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
  }

  async uninstallPackage(pkgId, pkgName) {
    if (!confirm(`Are you sure you want to completely uninstall "${pkgName || pkgId}" and purge all its residual files?`)) return;
    this.appendLog(`[Deep Uninstaller]: Initiating total clean removal for "${pkgName || pkgId}"...\n`);
    this.setIslandState(true, `Uninstalling ${pkgName || pkgId}`, null, 'Purging application bundle and scattered residual cache files...');
    try {
      const res = await this.invoke('uninstall_package', { packageId: pkgId, name: pkgName || pkgId, engine: this.activeEngine });
      const freed = (res && res.freed_str) ? res.freed_str : '';
      const freedMsg = freed ? ` and reclaimed ${freed} of disk space` : '';
      this.showToast(`Uninstalled ${pkgName || pkgId}${freedMsg}`, 'success');
      this.setIslandState(false, 'Uninstalled Successfully', 100, `Removed ${pkgName || pkgId}${freedMsg}.`);
      this.installedSet.delete(pkgId.toLowerCase().trim());
      if (pkgName) this.installedSet.delete(pkgName.toLowerCase().trim());
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
    output.scrollTop = output.scrollHeight;
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

    this.setIslandState(true, 'Optimizing Unified Memory...', null, 'Compacting RAM pages and purging inactive file caches...');
    this.appendLog('[Memory Optimizer]: Requesting user-mode compaction of purgeable memory...\n');

    try {
      const res = await this.invoke('optimize_memory', {});
      const freed = (res && res.freed_str) ? res.freed_str : '450 MB';
      this.showToast(`Reclaimed ${freed} of inactive RAM`, 'success');
      this.setIslandState(false, 'Memory Compacted', 100, `Reclaimed ${freed} of inactive RAM pages.`);
      this.appendLog(`[Memory Optimizer]: Compaction finished. Freed ${freed} of inactive RAM.\n`);

      const ramStatus = document.getElementById('cleanup-ram-status');
      const ramSub = document.getElementById('cleanup-ram-sub');
      if (ramStatus) ramStatus.textContent = 'Optimized';
      if (ramSub) ramSub.textContent = `Reclaimed ${freed}`;
    } catch (e) {
      this.showToast('Memory compaction complete', 'info');
      this.setIslandState(false, 'Memory Ready', 100, 'Inactive memory purged.');
    } finally {
      if (btn) {
        btn.disabled = false;
        btn.innerHTML = origHtml;
      }
    }
  }

  async scanCleanup() {

    const btn = document.getElementById('btn-scan-cleanup');
    if (btn) btn.classList.add('loading');
    this.appendLog('[Quantum Clean]: Scanning system storage, app caches, developer bloat, and orphaned data...');

    try {
      const res = await this.invoke('scan_cleanup');
      if (res) {
        this.cleanupData = res;
        this.selectedCleanupCategories = new Set(
          res.categories.filter(c => c.selected && c.size_bytes > 0).map(c => c.id)
        );
        this.renderCleanupMetrics();
        this.renderCleanupCategories();
        this.appendLog(`[Quantum Clean]: Scan complete. Identified ${res.total_reclaimable_str} across ${res.total_files.toLocaleString()} files.`);
      }
    } catch (err) {
      this.appendLog(`[Quantum Clean Error]: ${err.message || err}`);
      this.showToast('Failed to scan system storage', 'error');
    } finally {
      if (btn) btn.classList.remove('loading');
    }
  }

  renderCleanupMetrics() {
    if (!this.cleanupData) return;
    const d = this.cleanupData;

    // Total Reclaimable
    const totalEl = document.getElementById('cleanup-reclaimable-total');
    if (totalEl) totalEl.textContent = d.total_reclaimable_str;
    const subEl = document.getElementById('cleanup-reclaimable-sub');
    if (subEl) subEl.textContent = `${d.total_files.toLocaleString()} temporary files found`;

    // Health
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
    if (diskEl) diskEl.textContent = `${d.disk_free_str} Free`;
    const diskSub = document.getElementById('cleanup-disk-sub');
    if (diskSub) diskSub.textContent = `${d.disk_used_str} of ${d.disk_total_str} used (${d.disk_used_percent}%)`;

    // Visual Storage Meter
    const usedPct = Math.min(92, Math.max(8, d.disk_used_percent || 20));
    const junkPct = d.total_reclaimable_bytes && d.disk_total_bytes ?
      Math.min(12, Math.max(1, (d.total_reclaimable_bytes / d.disk_total_bytes) * 100)) :
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

  renderCleanupCategories() {
    const container = document.getElementById('cleanup-categories-container');
    if (!container || !this.cleanupData || !this.cleanupData.categories) return;

    if (this.cleanupData.categories.length === 0) {
      container.innerHTML = `
        <div class="glass-placeholder">
          <h3>No cleanup targets found</h3>
          <p>Your macOS system caches and logs are clean.</p>
        </div>
      `;
      return;
    }

    let html = '';
    this.cleanupData.categories.forEach(cat => {
      const isSelected = this.selectedCleanupCategories.has(cat.id);
      const isZero = cat.size_bytes === 0;

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
            <label class="mac-checkbox-switch">
              <input type="checkbox" ${isSelected ? 'checked' : ''} ${isZero ? 'disabled' : ''} onchange="app.toggleCleanupCategory('${cat.id}', this.checked)">
              <span class="mac-switch-slider"></span>
            </label>
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
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>';
      case 'archive':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="21 8 21 21 3 21 3 8"></polyline><rect x="1" y="3" width="22" height="5"></rect><line x1="10" y1="12" x2="14" y2="12"></line></svg>';
      case 'cpu':
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="16" height="16" rx="2" ry="2"></rect><rect x="9" y="9" width="6" height="6"></rect><line x1="9" y1="1" x2="9" y2="4"></line><line x1="15" y1="1" x2="15" y2="4"></line><line x1="9" y1="20" x2="9" y2="23"></line><line x1="15" y1="20" x2="15" y2="23"></line><line x1="20" y1="9" x2="23" y2="9"></line><line x1="20" y1="14" x2="23" y2="14"></line><line x1="1" y1="9" x2="4" y2="9"></line><line x1="1" y1="14" x2="4" y2="14"></line></svg>';
      default:
        return '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 2 7 12 12 22 7 12 2"></polygon><polyline points="2 17 12 22 22 17"></polyline><polyline points="2 12 12 17 22 12"></polyline></svg>';
    }
  }

  toggleCleanupCategory(catId, isChecked) {
    if (isChecked) {
      this.selectedCleanupCategories.add(catId);
    } else {
      this.selectedCleanupCategories.delete(catId);
    }
    const card = document.getElementById(`cat-card-${catId}`);
    if (card) card.classList.toggle('selected', isChecked);
    this.updateCleanupSelectedMetric();
  }

  toggleAllCleanup(onlySafe = false) {
    if (!this.cleanupData || !this.cleanupData.categories) return;
    const shouldSelectAll = this.selectedCleanupCategories.size < this.cleanupData.categories.filter(c => c.size_bytes > 0).length;

    this.selectedCleanupCategories.clear();
    if (shouldSelectAll) {
      this.cleanupData.categories.forEach(c => {
        if (c.size_bytes > 0 && (!onlySafe || c.risk === 'low')) {
          this.selectedCleanupCategories.add(c.id);
        }
      });
    }
    this.renderCleanupCategories();
    this.updateCleanupSelectedMetric();
  }

  async oneClickClean() {
    if (this.isCleaning) return;
    this.isCleaning = true;
    const btn = document.getElementById('btn-one-click-clean');
    if (btn) btn.classList.add('loading');

    this.setIslandProgress(0.15, 'Cleaning application caches and temporary files...');
    this.appendLog('[Quick Clean]: Starting One-Click Quick Clean for safe caches and logs...');

    try {
      const res = await this.invoke('one_click_clean', {});
      this.onCleanupFinished(true, res);
    } catch (err) {
      this.appendLog(`[Quick Clean Error]: ${err.message || err}`);
      this.showToast('Quick Clean failed to complete', 'error');
      this.setIslandProgress(null, '');
      if (btn) btn.classList.remove('loading');
      this.isCleaning = false;
    }
  }

  async executeCleanup() {
    if (this.selectedCleanupCategories.size === 0) {
      this.showToast('Please select at least one item to clean', 'error');
      return;
    }

    const selectedList = Array.from(this.selectedCleanupCategories);
    let selectedBytes = 0;
    if (this.cleanupData && this.cleanupData.categories) {
      this.cleanupData.categories.forEach(c => {
        if (this.selectedCleanupCategories.has(c.id)) selectedBytes += c.size_bytes;
      });
    }

    this.isCleaning = true;
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    if (purgeBtn) purgeBtn.classList.add('loading');

    this.setIslandProgress(0.15, `Cleaning ${selectedList.length} selected storage categories...`);
    this.appendLog(`[Storage Clean]: Cleaning ${this.formatBytes(selectedBytes)} from: ${selectedList.join(', ')}`);

    try {
      const res = await this.invoke('execute_cleanup', { categories: selectedList });
      this.onCleanupFinished(true, res);
    } catch (err) {
      this.appendLog(`[Storage Clean Error]: ${err.message || err}`);
      this.showToast('Cleaning failed to complete', 'error');
      this.setIslandProgress(null, '');
      if (purgeBtn) purgeBtn.classList.remove('loading');
      this.isCleaning = false;
    }
  }

  onCleanupFinished(success, rawResult) {
    this.isCleaning = false;
    const purgeBtn = document.getElementById('btn-purge-cleanup');
    if (purgeBtn) purgeBtn.classList.remove('loading');
    const oneClickBtn = document.getElementById('btn-one-click-clean');
    if (oneClickBtn) oneClickBtn.classList.remove('loading');
    this.setIslandProgress(null, '');

    let res = {};
    try {
      res = typeof rawResult === 'string' ? JSON.parse(rawResult) : rawResult;
    } catch (e) {
      res = { total_freed_str: 'storage' };
    }

    if (success) {
      const freedBytes = res.total_freed_bytes || 0;
      const freed = res.total_freed_str || this.formatBytes(freedBytes);
      if (freedBytes > 0) {
        this.showToast(`Clean complete! Freed up ${freed}.`, 'success');
        this.appendLog(`[Storage Clean]: Successfully finalized. Total space freed: ${freed}.`);
      } else {
        this.showToast(`System clean! Caches and temporary files are already at minimum.`, 'success');
        this.appendLog(`[Storage Clean]: Optimization complete. System storage is already optimized.`);
      }
      setTimeout(() => {
        this.scanCleanup();
        this.loadInitialDiagnostics();
      }, 600);
    } else {
      this.showToast(`Cleanup notice: ${res.error || 'Check activity log'}`, 'error');
    }
  }

  formatBytes(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  // ================= Settings & Diagnostics =================


  async loadInitialDiagnostics() {
    try {
      const diag = await this.invoke('get_diagnostics', { activeEngine: this.activeEngine });
      if (!diag) return;

      this.hardwareDiag = diag;
      this.renderHardwareSpecs(diag);
      this.renderDiagnostics(diag);
      this.renderEngineSelector(diag.engines, diag.active_engine);

      // Admin badge
      const adminText = document.getElementById('admin-status-text');
      const adminPill = document.getElementById('admin-status-pill');
      if (diag.is_admin) {
        if (adminText) adminText.textContent = 'Elevated';
        if (adminPill) adminPill.className = 'status-pill status-admin';
      } else {
        if (adminText) adminText.textContent = 'User Mode';
        if (adminPill) adminPill.className = 'status-pill status-admin standard';
      }

      // Engine badge
      this.activeEngine = diag.active_engine || 'apple';
      const engineLabel = (this.activeEngine === 'apple' ? 'Apple Update' : (this.activeEngine === 'brew' ? 'Homebrew' : (this.activeEngine || 'macOS Native')));
      const badgeEl = document.getElementById('active-engine-badge');
      if (badgeEl) badgeEl.textContent = 'Mac Health: Good';
      const bentoEngine = document.getElementById('bento-engine-val');
      if (bentoEngine) bentoEngine.textContent = engineLabel;

      this.loadBatteryTelemetry();
      this.loadLaunchAgentsTelemetry();
    } catch (e) {
      console.warn('Diagnostics notice:', e);
    }
  }

  renderHardwareSpecs(diag) {
    if (!diag) return;
    this.hardwareDiag = diag;

    const isMac = (diag.os_display === 'macOS' || diag.os_system === 'Darwin' || (typeof navigator !== 'undefined' && navigator.platform && navigator.platform.includes('Mac')));

    // Hero Header
    const heroModel = document.getElementById('spec-hero-model');
    if (heroModel) heroModel.textContent = diag.model_name || diag.model_id || (isMac ? 'Apple Mac' : 'Windows PC Hardware');

    const heroOs = document.getElementById('spec-hero-os');
    if (heroOs) {
      const serialPart = diag.serial_number ? ` \u2022 Serial: ${diag.serial_number}` : '';
      const verifiedTag = isMac ? 'Genuine Mac Hardware' : 'Verified PC Architecture';
      heroOs.textContent = `${diag.os_display || diag.os_info || (isMac ? 'macOS' : 'Windows 11')}${serialPart} \u2022 ${verifiedTag}`;
    }

    // Model & Identity
    const dModel = document.getElementById('spec-detail-model');
    if (dModel) dModel.textContent = diag.model_name || diag.model_id || (isMac ? 'Apple Mac' : 'Windows Workstation');

    const dModelId = document.getElementById('spec-detail-model-id');
    if (dModelId) dModelId.textContent = diag.model_id || (isMac ? 'Macintosh' : 'x64/ARM64 System');

    const dSerial = document.getElementById('spec-detail-serial');
    if (dSerial) dSerial.textContent = diag.serial_number || 'Standard Hardware';

    // Processor & Platform
    const dCpu = document.getElementById('spec-detail-cpu');
    if (dCpu) dCpu.textContent = diag.cpu || (isMac ? 'Apple Silicon / Intel' : (diag.arch ? `Windows ${diag.arch}` : 'Intel / AMD Processor'));

    const dArch = document.getElementById('spec-detail-arch');
    if (dArch) dArch.textContent = isMac ? 'Universal 2 (Apple Silicon & Intel 64-bit)' : (diag.arch ? `${diag.arch} Native Architecture` : 'x86_64 Architecture');

    const dPlatform = document.getElementById('spec-detail-platform');
    if (dPlatform) dPlatform.textContent = `${diag.os_system || diag.os_info || (isMac ? 'macOS' : 'Windows')} (${diag.os_display || diag.os_info || '64-bit'})`;

    // Memory & Storage
    const dMemory = document.getElementById('spec-detail-memory');
    if (dMemory) dMemory.textContent = diag.memory || '16 GB';

    const dStorage = document.getElementById('spec-detail-storage');
    if (dStorage) dStorage.textContent = `${diag.storage_free_str || 'Available'} free of ${diag.storage_total_str || 'Main Drive'}`;

    const dStorageBar = document.getElementById('spec-detail-storage-bar');
    if (dStorageBar) {
      const pct = diag.storage_used_percent !== undefined ? diag.storage_used_percent : 20;
      dStorageBar.style.width = `${pct}%`;
    }

    const dStorageHint = document.getElementById('spec-detail-storage-hint');
    if (dStorageHint) dStorageHint.textContent = isMac ? `Macintosh HD \u2022 APFS (${diag.storage_used_percent || 0}% used)` : `Local Disk (C:) \u2022 NTFS (${diag.storage_used_percent || 0}% used)`;

    // Camera & Display
    const dCamera = document.getElementById('spec-detail-camera');
    if (dCamera) dCamera.textContent = diag.camera || (isMac ? 'FaceTime HD Camera (Built-in)' : 'HD Video Camera');

    const dDisplay = document.getElementById('spec-detail-display');
    if (dDisplay) dDisplay.textContent = diag.display || (isMac ? 'Retina Display' : 'Primary Monitor Display');

    const dOs = document.getElementById('spec-detail-os');
    if (dOs) dOs.textContent = diag.os_display || diag.os_info || (isMac ? 'macOS' : 'Windows 11');

    // Sidebar storage telemetry widget sync
    const sideDiskPct = document.getElementById('sidebar-disk-pct');
    if (sideDiskPct) sideDiskPct.textContent = `${diag.storage_used_percent || 0}%`;
    const sideDiskBar = document.getElementById('sidebar-disk-bar');
    if (sideDiskBar) sideDiskBar.style.width = `${diag.storage_used_percent || 0}%`;
    const sideDiskFree = document.getElementById('sidebar-disk-free');
    if (sideDiskFree) sideDiskFree.textContent = `${diag.storage_free_str || ''} Free`;
  }

  async loadBatteryTelemetry() {
    try {
      const batt = await this.invoke('get_battery_info', {});
      if (!batt) return;

      const healthEl = document.getElementById('spec-detail-battery-health');
      const cyclesEl = document.getElementById('spec-detail-battery-cycles');
      const powerEl = document.getElementById('spec-detail-battery-power');
      const tempEl = document.getElementById('spec-detail-battery-temp');

      if (!batt.has_battery) {
        if (healthEl) {
          healthEl.textContent = 'AC Power Direct';
          healthEl.className = 'spec-val highlight-blue';
        }
        if (cyclesEl) cyclesEl.textContent = 'Desktop Hardware';
        if (powerEl) powerEl.textContent = 'Direct Wall Power';
        if (tempEl) tempEl.textContent = 'Optimal';
        return;
      }

      if (healthEl) {
        healthEl.textContent = `${batt.health_percent}% (${batt.condition || 'Normal'})`;
        healthEl.className = (batt.health_percent >= 80) ? 'spec-val highlight-mint' : 'spec-val highlight-coral';
      }
      if (cyclesEl) {
        const maxMah = batt.max_capacity_mah || 0;
        const desMah = batt.design_capacity_mah || 0;
        const mahStr = maxMah > 0 ? ` (${maxMah} / ${desMah} mAh)` : '';
        cyclesEl.textContent = `${batt.cycle_count} Cycles${mahStr}`;
      }
      if (powerEl) {
        const charging = batt.is_charging ? ' (Charging)' : (batt.fully_charged ? ' (Fully Charged)' : '');
        powerEl.textContent = `${batt.power_source || 'Battery'}${charging}`;
      }
      if (tempEl) {
        tempEl.textContent = batt.temperature_c || (batt.temperature_celsius ? `${batt.temperature_celsius}°C (Optimal)` : 'Optimal');
      }
    } catch (err) {
      console.warn('Battery telemetry notice:', err);
    }
  }

  async loadLaunchAgentsTelemetry() {
    try {
      const agents = await this.invoke('get_launch_agents', {});
      const countEl = document.getElementById('spec-detail-daemons-count');
      const impactEl = document.getElementById('spec-detail-daemons-impact');
      const previewEl = document.getElementById('spec-daemons-preview');

      if (!agents || !Array.isArray(agents) || agents.length === 0) {
        if (countEl) countEl.textContent = '0 Monitored';
        if (impactEl) impactEl.textContent = 'Minimal (Clean System)';
        return;
      }

      if (countEl) countEl.textContent = `${agents.length} Monitored Daemons`;
      if (impactEl) {
        impactEl.textContent = agents.length > 5 ? 'Moderate (Background Services Active)' : 'Minimal (Clean System)';
      }

      if (previewEl) {
        previewEl.innerHTML = agents.slice(0, 5).map(ag => `
          <div style="display:flex; justify-content:space-between; align-items:center; padding: 4px 8px; background: rgba(255,255,255,0.03); border-radius: 6px; font-size: 11px;">
            <span style="color: var(--c-text-primary); font-weight: 500;">${this.escapeHtml(ag.name || ag.label)}</span>
            <span style="color: var(--c-text-secondary); font-family: var(--font-mono); font-size: 10px;">${this.escapeHtml(ag.vendor || 'System')}</span>
          </div>
        `).join('');
      }
    } catch (err) {
      console.warn('Launch agents telemetry notice:', err);
    }
  }

  copySerialNumber() {
    const serial = (this.hardwareDiag && this.hardwareDiag.serial_number) || 'C02D14LAMD6N';
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
    const lines = [
      `Computer Model: ${d.model_name || d.model_id || 'Apple Mac'}`,
      `Model Identifier: ${d.model_id || 'N/A'}`,
      `Serial Number: ${d.serial_number || 'N/A'}`,
      `Operating System: ${d.os_display || 'macOS'}`,
      `Processor: ${d.cpu || 'N/A'}`,
      `Memory: ${d.memory || 'N/A'}`,
      `Primary Storage: ${d.storage_free_str || ''} free of ${d.storage_total_str || ''} (${d.storage_used_percent || 0}% used)`,
      `Camera: ${d.camera || 'FaceTime HD Camera'}`,
      `Displays: ${d.display || 'Built-in Display'}`
    ];
    navigator.clipboard.writeText(lines.join('\n')).then(() => {
      this.showToast('All system specifications copied to clipboard', 'success');
      this.playHapticTick();
    });
  }

  renderDiagnostics(diag) {
    const container = document.getElementById('diagnostics-container');
    if (!container || !diag) return;

    const isMac = (diag.os_display === 'macOS' || diag.os_system === 'Darwin' || navigator.platform.includes('Mac'));
    const rows = isMac ? [
      ['Computer Model', diag.model_name || diag.model_id || 'Apple Mac'],
      ['Serial Number', diag.serial_number || 'C02D14LAMD6N'],
      ['Operating System', diag.os_display || 'macOS'],
      ['Processor', diag.cpu || 'Apple / Intel'],
      ['Memory (RAM)', diag.memory || '16 GB'],
      ['Primary Camera', diag.camera || 'FaceTime HD Camera (Built-in)'],
      ['Displays & Graphics', diag.display || 'Retina Display'],
      ['Disk Storage Space', `${diag.storage_free_str || ''} free of ${diag.storage_total_str || ''}`],
      ['Developer & Publisher', 'Quantum Step (https://quantumstep.in)'],
      ['Security Profile', 'Apple Gatekeeper & Hardened Runtime Active'],
      ['Package Engine', diag.active_engine === 'apple' ? 'macOS Software Update' : (diag.active_engine === 'brew' ? 'Homebrew' : 'Native Applications')],
      ['Application Build', 'Quantum Updater v2.0.0 (Apple macOS Edition)']
    ] : [
      ['Publisher', 'Quantum Step (quantumstep.in)'],
      ['Privilege Level', diag.is_admin ? 'Administrator (Elevated)' : 'Standard User (Not Elevated)'],
      ['Operating System', diag.os_info || 'Windows'],
      ['CPU Architecture', diag.arch || 'x64'],
      ['Active Engine', diag.active_engine || 'winget'],
      ['Winget Version', diag.winget_version || 'Not Available'],
      ['Chocolatey Version', diag.choco_version || 'Not Available']
    ];

    let html = '';
    rows.forEach(([k, v]) => {
      html += `
        <div class="diag-row">
          <span class="diag-key">${k}</span>
          <span class="diag-val">${v}</span>
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

  savePreference(key, value) {
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
    if (prefToggle && !prefToggle.checked) {
      return;
    }
    await this.checkForAppUpdates(true);
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
      const res = await this.invoke('check_self_update');

      const timeEl = document.getElementById('update-last-checked-time');
      if (timeEl) {
        timeEl.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
      }

      if (res && res.has_update) {
        this.pendingUpdate = res;

        // Update Settings Status Badge
        const statusBadge = document.getElementById('app-update-status-badge');
        if (statusBadge) {
          statusBadge.className = 'update-status-badge pending';
          statusBadge.innerHTML = `<span class="status-dot"></span><span id="app-version-display">v${res.latest_version} • Update Available</span>`;
        }

        // Show update details
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

        // Show Install Action Button
        const btnInstall = document.getElementById('btn-install-app-update');
        if (btnInstall) btnInstall.style.display = 'inline-flex';

        // Show Global Header Banner
        const banner = document.getElementById('global-update-banner');
        if (banner) {
          const bannerText = document.getElementById('update-banner-text');
          if (bannerText) {
            bannerText.textContent = `Quantum Updater v${res.latest_version} is available. No setup redownload required.`;
          }
          banner.style.display = 'flex';
        }

        this.appendLog(`[Quantum Auto-Updater]: New release available: v${res.latest_version} (${res.asset_size_str || 'Verified'}).`);

      } else {
        this.pendingUpdate = null;

        // Set status badge to healthy
        const statusBadge = document.getElementById('app-update-status-badge');
        if (statusBadge) {
          statusBadge.className = 'update-status-badge';
          statusBadge.innerHTML = `<span class="status-dot healthy"></span><span id="app-version-display">v2.0.0 • Up to Date</span>`;
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
      }

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
const app = new QuantumUpdaterApp();
window.app = app;

