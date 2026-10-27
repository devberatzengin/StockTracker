import { Alert, Button, Group, Paper, Select, Stack, Text, TextInput, Title } from '@mantine/core'
import { IconFileSpreadsheet } from '@tabler/icons-react'
import { useMutation } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { apiClient, getErrorMessage } from '@/api/client'
import type { AssignmentStatus } from '@/api/types'
import { ASSIGNMENT_STATUS_LABELS } from '@/features/assignments/labels'
import { todayIstanbul } from '@/lib/dates'

/** Downloads an Excel report with the access token and saves it under `fileName`. */
function useDownload() {
  return useMutation({
    mutationFn: async ({ path, fileName }: { path: string; fileName: string }) => {
      const file = await apiClient.getForBlob(path)
      const url = URL.createObjectURL(file)
      const link = document.createElement('a')
      link.href = url
      link.download = fileName
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 10_000)
    },
    meta: { skipGlobalError: true },
  })
}

function ReportCard({
  title,
  description,
  children,
  onDownload,
  loading,
}: {
  title: string
  description: string
  children?: ReactNode
  onDownload: () => void
  loading: boolean
}) {
  return (
    <Paper withBorder p="md" radius="md">
      <Stack gap="sm">
        <div>
          <Text fw={600}>{title}</Text>
          <Text size="sm" c="dimmed">
            {description}
          </Text>
        </div>
        {children}
        <Group>
          <Button
            leftSection={<IconFileSpreadsheet size={16} />}
            onClick={onDownload}
            loading={loading}
          >
            Excel indir
          </Button>
        </Group>
      </Stack>
    </Paper>
  )
}

export function ReportsPage() {
  const download = useDownload()
  const [from, setFrom] = useState(todayIstanbul(-29))
  const [to, setTo] = useState(todayIstanbul())
  const [status, setStatus] = useState<AssignmentStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const today = todayIstanbul()

  const run = async (path: string, fileName: string) => {
    setError(null)
    try {
      await download.mutateAsync({ path, fileName })
    } catch (e) {
      setError(getErrorMessage(e))
    }
  }
  const busy = (path: string) => download.isPending && download.variables?.path.startsWith(path)

  return (
    <Stack maw={720}>
      <Title order={2}>Raporlar</Title>
      <Text size="sm" c="dimmed">
        Tarihler İstanbul saatiyle yazılır. Bir rapor en fazla 50.000 satır içerebilir.
      </Text>
      {error && (
        <Alert color="red" withCloseButton onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <ReportCard
        title="Stok durumu"
        description="Ürün başına toplam, minimum ve kritik durumu; ikinci sayfada lokasyon bazında dağılım."
        onDownload={() => void run('/reports/stock.xlsx', `stok-durumu-${today}.xlsx`)}
        loading={!!busy('/reports/stock')}
      />

      <ReportCard
        title="Hareket dökümü"
        description="Seçilen tarih aralığındaki tüm hareketler (iki gün dahil)."
        onDownload={() =>
          void run(`/reports/movements.xlsx?from=${from}&to=${to}`, `hareketler-${from}_${to}.xlsx`)
        }
        loading={!!busy('/reports/movements')}
      >
        <Group>
          <TextInput
            type="date"
            label="Başlangıç"
            value={from}
            max={to}
            onChange={(e) => setFrom(e.currentTarget.value)}
          />
          <TextInput
            type="date"
            label="Bitiş"
            value={to}
            min={from}
            onChange={(e) => setTo(e.currentTarget.value)}
          />
        </Group>
      </ReportCard>

      <ReportCard
        title="Zimmet listesi"
        description="Adet, alan kişi, departman, veriliş, teslim, iade ve durum."
        onDownload={() =>
          void run(
            `/reports/assignments.xlsx${status ? `?status=${status}` : ''}`,
            `zimmetler-${today}.xlsx`,
          )
        }
        loading={!!busy('/reports/assignments')}
      >
        <Select
          label="Durum"
          placeholder="Tümü"
          data={(Object.keys(ASSIGNMENT_STATUS_LABELS) as AssignmentStatus[]).map((s) => ({
            value: s,
            label: ASSIGNMENT_STATUS_LABELS[s],
          }))}
          value={status}
          onChange={(value) => setStatus(value as AssignmentStatus | null)}
          clearable
          w={220}
        />
      </ReportCard>
    </Stack>
  )
}
