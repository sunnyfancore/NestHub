import { http } from '@/api/http'

export interface SmtpConfig {
  host: string
  port: number
  useSsl: boolean
  username: string
  hasPassword: boolean
  fromEmail: string
  fromName: string
}

export async function getSmtpConfig(): Promise<SmtpConfig> {
  const { data } = await http.get<SmtpConfig>('/security/smtp')
  return data
}

export async function updateSmtpConfig(payload: {
  host?: string
  port: number
  useSsl: boolean
  username?: string
  password?: string
  fromEmail?: string
  fromName?: string
}): Promise<void> {
  await http.put('/security/smtp', payload)
}

export async function testSmtp(email: string): Promise<void> {
  await http.post('/security/smtp/test', { email })
}
