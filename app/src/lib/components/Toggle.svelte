<script lang="ts">
  let { checked, onchange, label }: { checked: boolean; onchange: (v: boolean) => void; label: string } = $props();
</script>

<button class="toggle" role="switch" aria-checked={checked} aria-label={label} class:on={checked} onclick={() => onchange(!checked)}>
  <span class="knob"></span>
</button>

<style>
  .toggle { width: 40px; height: 24px; border-radius: 999px; background: var(--hairline-strong); position: relative; flex: none; transition: background-color 180ms ease; }
  .toggle.on { background: var(--accent); }
  .knob {
    position: absolute; top: 3px; left: 3px; width: 18px; height: 18px; border-radius: 50%;
    background: #fff; box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
    transition: transform 260ms var(--spring);
  }
  .on .knob { transform: translateX(16px); }
</style>
