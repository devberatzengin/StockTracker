import type { TreeNodeData } from '@mantine/core'
import type { LocationTreeNode } from '@/api/types'

export const PATH_SEPARATOR = ' › '

export interface FlatLocation {
  node: LocationTreeNode
  /** Names from the root down to this node, e.g. "Bina › Oda › Dolap". */
  pathLabel: string
  /** Ids from the root down to and including this node. */
  pathIds: string[]
}

export function flattenTree(
  nodes: LocationTreeNode[],
  parents: FlatLocation | null = null,
): FlatLocation[] {
  return nodes.flatMap((node) => {
    const flat: FlatLocation = {
      node,
      pathLabel: parents ? `${parents.pathLabel}${PATH_SEPARATOR}${node.name}` : node.name,
      pathIds: [...(parents?.pathIds ?? []), node.id],
    }
    return [flat, ...flattenTree(node.children, flat)]
  })
}

/** Keeps nodes whose name or code matches, plus their ancestors (so matches stay in context). */
export function filterTree(nodes: LocationTreeNode[], search: string): LocationTreeNode[] {
  const term = search.trim().toLocaleLowerCase('tr-TR')
  if (!term) return nodes

  return nodes.flatMap((node) => {
    const children = filterTree(node.children, term)
    const matches =
      node.name.toLocaleLowerCase('tr-TR').includes(term) ||
      node.code.toLocaleLowerCase('tr-TR').includes(term)
    return matches || children.length > 0 ? [{ ...node, children }] : []
  })
}

export function toTreeData(nodes: LocationTreeNode[]): TreeNodeData[] {
  return nodes.map((node) => ({
    value: node.id,
    label: node.name,
    nodeProps: { code: node.code, type: node.type },
    children: node.children.length > 0 ? toTreeData(node.children) : undefined,
  }))
}

/** Select value standing for "no parent" when moving a location. */
export const ROOT_VALUE = '__root__'

/**
 * Target options for moving the given location: every location except itself and its
 * subtree (which would create a cycle), plus the root.
 */
export function moveTargetOptions(location: FlatLocation, tree: LocationTreeNode[]) {
  const currentParentId = location.pathIds.at(-2) ?? null
  return [
    { value: ROOT_VALUE, label: 'Kök (üst lokasyon yok)', disabled: currentParentId === null },
    ...flattenTree(tree).map((flat) => ({
      value: flat.node.id,
      label: flat.pathLabel,
      disabled: flat.pathIds.includes(location.node.id) || flat.node.id === currentParentId,
    })),
  ]
}
