<script lang="ts">
  let { size = 28 }: { size?: number } = $props();
  // Small sizes use the heavier drawing (brand/quantum-updater-icon-small.svg).
  const small = $derived(size <= 32);
</script>

<!-- Quantum Updater mark: the Quantum Step emblem without its text (brand/quantum-updater-icon.svg). -->
<svg width={size} height={size} viewBox="0 0 1000 1000" aria-hidden="true">
  {#if small}
    
      <defs>
        <linearGradient id="qsm-ring" x1="60" y1="0" x2="940" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0" stop-color="#00E5FF"/>
          <stop offset="0.3" stop-color="#3D8BFF"/>
          <stop offset="0.55" stop-color="#B14BFF"/>
          <stop offset="0.75" stop-color="#FF2BD6"/>
          <stop offset="1" stop-color="#FFB000"/>
        </linearGradient>
      </defs>
      <circle cx="500" cy="500" r="495" fill="#0B0614"/>
      <path d="M884.6,655.3 A415,415 0 1 1 884.6,344.7" fill="none" stroke="url(#qsm-ring)" stroke-width="90" stroke-linecap="round"/>
      <g fill="none" stroke="#7DF9FF" stroke-width="62">
        <ellipse cx="500" cy="500" rx="110" ry="275"/>
        <ellipse cx="500" cy="500" rx="110" ry="275" transform="rotate(60 500 500)"/>
        <ellipse cx="500" cy="500" rx="110" ry="275" transform="rotate(-60 500 500)"/>
      </g>
      <circle cx="500" cy="500" r="62" fill="#2F9BFF"/>
  {:else}
    
      <defs>
        <linearGradient id="qu-ring" x1="60" y1="0" x2="940" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0" stop-color="#00E5FF"/>
          <stop offset="0.28" stop-color="#3D8BFF"/>
          <stop offset="0.5" stop-color="#B14BFF"/>
          <stop offset="0.7" stop-color="#FF2BD6"/>
          <stop offset="0.86" stop-color="#FF7A45"/>
          <stop offset="1" stop-color="#FFD000"/>
        </linearGradient>
        <linearGradient id="qu-inner" x1="200" y1="0" x2="800" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0" stop-color="#2F9BFF"/>
          <stop offset="0.5" stop-color="#8B5CF6"/>
          <stop offset="1" stop-color="#FF2BD6"/>
        </linearGradient>
        <linearGradient id="qu-hook" x1="840" y1="0" x2="960" y2="0" gradientUnits="userSpaceOnUse">
          <stop offset="0" stop-color="#FF2BD6"/>
          <stop offset="1" stop-color="#FF8A3D"/>
        </linearGradient>
      </defs>
      <circle cx="500" cy="500" r="490" fill="#0B0614"/>
      <path d="M908.4,592.4 A420,420 0 1 1 908.4,407.6" fill="none" stroke="url(#qu-ring)" stroke-width="52" stroke-linecap="round"/>
      <path d="M928,538 A54,54 0 1 1 928,462" fill="none" stroke="url(#qu-hook)" stroke-width="40" stroke-linecap="round"/>
      <circle cx="890" cy="500" r="18" fill="#FF3DB8"/>
      <circle cx="500" cy="500" r="300" fill="none" stroke="url(#qu-inner)" stroke-width="26"/>
      <g fill="none" stroke="#7DF9FF" stroke-width="34">
        <ellipse cx="500" cy="500" rx="88" ry="226"/>
        <ellipse cx="500" cy="500" rx="88" ry="226" transform="rotate(60 500 500)"/>
        <ellipse cx="500" cy="500" rx="88" ry="226" transform="rotate(-60 500 500)"/>
      </g>
      <circle cx="500" cy="500" r="52" fill="#0B0614" stroke="#2F9BFF" stroke-width="30"/>
  {/if}
</svg>
