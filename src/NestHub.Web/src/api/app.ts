import { http } from '@/api/http'

export interface AppInfo {
  runtimeVersion: string
  appVersion: string
  categoryCount: number
  linkCount: number
  tenantName: string
}

export async function getAppInfo(): Promise<AppInfo> {
  const { data } = await http.get<AppInfo>('/app-info')
  return data
}
