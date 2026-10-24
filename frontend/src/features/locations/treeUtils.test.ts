import { describe, expect, it } from 'vitest'
import { sampleTree } from '@/features/locations/testTree'
import {
  filterTree,
  flattenTree,
  moveTargetOptions,
  ROOT_VALUE,
} from '@/features/locations/treeUtils'

const byId = (id: string) => flattenTree(sampleTree).find((f) => f.node.id === id)!

describe('flattenTree', () => {
  it('builds path labels and ids from the root', () => {
    const shelf = byId('rafa1')

    expect(shelf.pathLabel).toBe('Bina › Oda 101 › Dolap 3 › Raf A1')
    expect(shelf.pathIds).toEqual(['bina', 'oda', 'dolap', 'rafa1'])
  })
})

describe('filterTree', () => {
  it('keeps matches with their ancestors and drops other branches', () => {
    const result = filterTree(sampleTree, 'raf b')

    expect(flattenTree(result).map((f) => f.node.name)).toEqual([
      'Bina',
      'Oda 101',
      'Dolap 3',
      'Raf B2',
    ])
  })

  it('matches codes case-insensitively', () => {
    expect(flattenTree(filterTree(sampleTree, 'loc-kat2')).map((f) => f.node.id)).toEqual([
      'bina2',
      'kat2',
    ])
  })

  it('returns the full tree for an empty search', () => {
    expect(filterTree(sampleTree, '  ')).toBe(sampleTree)
  })
})

describe('moveTargetOptions', () => {
  it('disables the location itself, its subtree and its current parent', () => {
    const options = moveTargetOptions(byId('oda'), sampleTree)
    const disabled = options.filter((o) => o.disabled).map((o) => o.value)
    const enabled = options.filter((o) => !o.disabled).map((o) => o.value)

    expect(disabled).toEqual(['bina', 'oda', 'dolap', 'rafa1', 'rafb2'])
    expect(enabled).toEqual([ROOT_VALUE, 'bina2', 'kat2'])
  })

  it('disables the root option for a root location', () => {
    const root = moveTargetOptions(byId('bina2'), sampleTree).find((o) => o.value === ROOT_VALUE)!

    expect(root.disabled).toBe(true)
  })
})
