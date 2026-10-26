/** Moves the item at `from` to `to` (drag and drop, or the arrow buttons). */
export function move<T>(items: readonly T[], from: number, to: number): T[] {
  if (from === to || from < 0 || to < 0 || from >= items.length || to >= items.length)
    return [...items]
  const next = [...items]
  const [moved] = next.splice(from, 1)
  next.splice(to, 0, moved!)
  return next
}
