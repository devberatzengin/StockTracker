import { Group, Select, Stack, TextInput, Title } from '@mantine/core'
import { useState } from 'react'
import type { UserLookup } from '@/api/types'
import { UserPicker } from '@/features/assignments/UserPicker'
import { AuditHistory } from '@/features/audit/AuditHistory'
import { ENTITY_TYPE_OPTIONS } from '@/features/audit/auditLabels'
import { endOfDayIstanbul, startOfDayIstanbul } from '@/lib/dates'

/** Admin: every change in the system, filtered by record type, user and date. */
export function AuditPage() {
  const [entityType, setEntityType] = useState<string | null>(null)
  const [user, setUser] = useState<UserLookup | null>(null)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')

  return (
    <Stack maw={960}>
      <Title order={2}>Değişiklik kaydı</Title>
      <Group align="flex-end" gap="sm">
        <Select
          label="Kayıt tipi"
          placeholder="Tümü"
          data={ENTITY_TYPE_OPTIONS}
          value={entityType}
          onChange={setEntityType}
          clearable
        />
        <UserPicker label="Kullanıcı" value={user} onChange={setUser} />
        <TextInput
          type="date"
          label="Başlangıç"
          value={from}
          onChange={(e) => setFrom(e.currentTarget.value)}
        />
        <TextInput
          type="date"
          label="Bitiş"
          value={to}
          onChange={(e) => setTo(e.currentTarget.value)}
        />
      </Group>
      <AuditHistory
        key={`${entityType}-${user?.id}-${from}-${to}`}
        filters={{
          entityType,
          userId: user?.id,
          from: from ? startOfDayIstanbul(from) : null,
          to: to ? endOfDayIstanbul(to) : null,
        }}
        showEntity
      />
    </Stack>
  )
}
