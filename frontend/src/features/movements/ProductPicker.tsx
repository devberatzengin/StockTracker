import { ActionIcon, Alert, Group, Select, Tooltip } from '@mantine/core'
import { useDebouncedValue, useDisclosure } from '@mantine/hooks'
import { IconScan } from '@tabler/icons-react'
import { useState } from 'react'
import { getErrorMessage, isApiError } from '@/api/client'
import { useProducts } from '@/features/catalog/catalogApi'
import { ProductThumb } from '@/features/catalog/ProductThumb'
import { fetchPickedProduct, type PickedProduct } from '@/features/movements/pickedProduct'
import { lookupScan } from '@/features/scan/scanApi'
import { ScanDialog } from '@/features/scan/ScanDialog'

interface Props {
  value: PickedProduct | null
  onChange: (product: PickedProduct | null) => void
  error?: string
  label?: string
}

/** Searchable active-product select with barcode / product code scanning. */
export function ProductPicker({ value, onChange, error, label = 'Ürün' }: Props) {
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search.trim(), 250)
  const { data } = useProducts({ page: 1, pageSize: 20, search: debounced })
  const [scanning, scan] = useDisclosure()
  const [scanError, setScanError] = useState<string | null>(null)

  const results: PickedProduct[] = (data?.items ?? []).map((p) => ({
    id: p.id,
    code: p.code,
    name: p.name,
    trackingType: p.trackingType,
    unit: p.unit,
    thumbnailUrl: p.thumbnailUrl,
  }))
  // Keep the selected product selectable even when it is not in the current search results.
  const all = value && !results.some((p) => p.id === value.id) ? [value, ...results] : results

  const onScan = async (input: string) => {
    setScanError(null)
    try {
      const result = await lookupScan(input)
      if (result.type !== 'product') {
        setScanError(`"${result.title}" bir ürün değil. Ürün barkodunu ya da ürün kodunu okutun.`)
        return
      }
      onChange(await fetchPickedProduct(result.id))
      scan.close()
    } catch (e) {
      setScanError(
        isApiError(e, 404) ? 'Bu barkoda ya da koda ait ürün bulunamadı.' : getErrorMessage(e),
      )
    }
  }

  return (
    <>
      <Group align="flex-end" gap="xs" wrap="nowrap">
        <Select
          label={label}
          aria-label={label}
          placeholder="Ad, kod veya barkod"
          data={all.map((p) => ({ value: p.id, label: `${p.name} · ${p.code}` }))}
          value={value?.id ?? null}
          onChange={(id) => onChange(all.find((p) => p.id === id) ?? null)}
          searchable
          searchValue={search}
          onSearchChange={setSearch}
          filter={({ options }) => options}
          renderOption={({ option }) => (
            <Group gap="xs" wrap="nowrap">
              <ProductThumb url={all.find((p) => p.id === option.value)?.thumbnailUrl} size={28} />
              <span>{option.label}</span>
            </Group>
          )}
          nothingFoundMessage="Ürün bulunamadı"
          error={error}
          style={{ flex: 1 }}
        />
        <Tooltip label="Barkodu okut">
          <ActionIcon
            size={36}
            variant="light"
            aria-label={`${label} okut`}
            onClick={scan.open}
            mb={error ? 22 : 0}
          >
            <IconScan size={18} />
          </ActionIcon>
        </Tooltip>
      </Group>
      <ScanDialog
        opened={scanning}
        onClose={() => {
          setScanError(null)
          scan.close()
        }}
        onScan={onScan}
        title="Ürün okut"
      >
        {scanError && <Alert color="orange">{scanError}</Alert>}
      </ScanDialog>
    </>
  )
}
