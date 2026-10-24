import { Center, Paper, Text, Title } from '@mantine/core'

// Login form arrives with authentication (Phase 1.1).
export function LoginPage() {
  return (
    <Center mih="100vh" p="md">
      <Paper withBorder p="xl" w={360}>
        <Title order={3}>Giriş</Title>
        <Text c="dimmed" mt="xs">
          Giriş ekranı henüz hazır değil.
        </Text>
      </Paper>
    </Center>
  )
}
