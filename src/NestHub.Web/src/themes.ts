export const themeOptions = [
  { name: 'default2', title: '经典 · 简洁导航' },
  { name: 'ocean', title: '海洋 · 蔚蓝清爽' },
  { name: 'neon', title: '霓虹 · 未来科技' },
  { name: 'paper', title: '纸笺 · 温润书卷' },
  { name: 'sakura', title: '樱糖 · 二次元粉色' },
  { name: 'lime', title: '青柠 · 清新自然' },
  { name: 'blackgold', title: '曜金 · 高级黑金' },
  { name: 'liquid', title: '流光 · 液态玻璃' },
  { name: 'directory', title: '网址簿 · 经典目录' },
  { name: 'workspace', title: '效率台 · 侧栏工作台' },
  { name: 'launchpad', title: '起始页 · 极简导航' },
] as const

export function resolveTheme(value?: string | null): string {
  const aliases: Record<string, string> = { coral: 'default2', nord: 'ocean', glass: 'liquid', midnight: 'blackgold' }
  const name = aliases[value || ''] || value
  return themeOptions.some(theme => theme.name === name) ? name! : 'default2'
}
