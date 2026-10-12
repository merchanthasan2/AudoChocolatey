<script lang="ts">
  let { size = 28 }: { size?: number } = $props();
</script>

<!-- Quantum Updater mark: a ring that's almost closed, with the gap turned into an upward arrow. -->
<svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true">
  <rect width="32" height="32" rx="9" fill="var(--accent)" />
  <path d="M22.4 11.2A8 8 0 1 0 24 16" fill="none" stroke="var(--accent-ink)" stroke-width="2.6" stroke-linecap="round" />
  <path d="M19.6 8.6h4v4" fill="none" stroke="var(--accent-ink)" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" />
</svg>
