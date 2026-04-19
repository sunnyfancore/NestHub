import { http } from '@/api/http'

export interface TransitionSetting {
  isEnabled: boolean
  visitorStaySeconds: number
  adminStaySeconds: number
  adScript1?: string | null
  adScript2?: string | null
}

export async function getTransitionSetting(): Promise<TransitionSetting> {
  const { data } = await http.get<TransitionSetting>('/transition')
  return data
}

export async function updateTransitionSetting(payload: TransitionSetting): Promise<TransitionSetting> {
  const { data } = await http.put<TransitionSetting>('/transition', payload)
  return data
}
