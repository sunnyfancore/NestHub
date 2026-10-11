import { nextTick, onBeforeUnmount, onMounted, watch, type Ref } from 'vue'

/** Keep keyboard navigation inside an open mobile sidebar. Desktop navigation stays unchanged. */
export function useSidebarKeyboard(open: Ref<boolean>, sidebar: Ref<HTMLElement | null>, maxWidth: number) {
  const isActive = () => open.value && window.innerWidth <= maxWidth && sidebar.value
  const focusable = () => Array.from(sidebar.value?.querySelectorAll<HTMLElement>(
    'a[href],button:not([disabled]),input:not([disabled]),[tabindex="0"]',
  ) || []).filter(element => element.getClientRects().length > 0 && getComputedStyle(element).visibility !== 'hidden')

  watch(open, async value => {
    if (!value) return
    await nextTick()
    if (isActive()) focusable()[0]?.focus()
  }, { flush: 'post' })

  function handleTab(event: KeyboardEvent) {
    if (event.key !== 'Tab' || !isActive()) return
    if (Array.from(document.querySelectorAll<HTMLElement>('.el-dialog,.el-message-box')).some(element => element.getClientRects().length)) return
    const elements = focusable()
    if (!elements.length) return
    const first = elements[0]!, last = elements[elements.length - 1]!
    const focused = document.activeElement
    if (!sidebar.value?.contains(focused) || (event.shiftKey ? focused === first : focused === last)) {
      event.preventDefault()
      const target = event.shiftKey ? last : first
      target.focus()
    }
  }
  onMounted(() => window.addEventListener('keydown', handleTab))
  onBeforeUnmount(() => window.removeEventListener('keydown', handleTab))
}
