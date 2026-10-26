import { Select } from '@mantine/core'
import { useDebouncedValue } from '@mantine/hooks'
import { useMemo, useState } from 'react'
import type { UserLookup } from '@/api/types'
import { useUserLookup } from '@/features/assignments/assignmentsApi'

interface Props {
  value: UserLookup | null
  onChange: (user: UserLookup | null) => void
  label?: string
  placeholder?: string
  error?: string
  size?: string
}

const describe = (u: UserLookup) => (u.department ? `${u.fullName} · ${u.department}` : u.fullName)

/** Active users searched by name or department (remote search). */
export function UserPicker({
  value,
  onChange,
  label = 'Kişi',
  placeholder = 'Ad ya da departman ara',
  error,
  size,
}: Props) {
  const [search, setSearch] = useState('')
  const [debounced] = useDebouncedValue(search, 250)
  const { data = [], isFetching } = useUserLookup(debounced.trim())

  // Keep the selected user selectable even when the current search does not return it.
  const users = useMemo(
    () => (value && !data.some((u) => u.id === value.id) ? [value, ...data] : data),
    [data, value],
  )

  return (
    <Select
      label={label}
      placeholder={placeholder}
      data={users.map((u) => ({ value: u.id, label: describe(u) }))}
      value={value?.id ?? null}
      onChange={(id) => onChange(users.find((u) => u.id === id) ?? null)}
      searchable
      clearable
      filter={({ options }) => options}
      onSearchChange={(text) => {
        // The search box shows the selected label; only typed text is a query.
        if (!value || text !== describe(value)) setSearch(text)
      }}
      nothingFoundMessage={isFetching ? 'Aranıyor…' : 'Kullanıcı bulunamadı'}
      error={error}
      size={size}
    />
  )
}
