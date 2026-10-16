import { useEffect, useEffectEvent, useState } from 'react'
import { apiFetch } from '../lib/api'

/** Keeps a previous request from replacing the data for the current filters. */
export function useApiQuery<T>(url: string, onError: () => void, revision = 0) {
  const key = `${url}:${revision}`
  const [result, setResult] = useState<{ key: string; data: T | null } | null>(null)
  const reportError = useEffectEvent(onError)

  useEffect(() => {
    const controller = new AbortController()
    let active = true

    async function load() {
      try {
        const response = await apiFetch(url, { signal: controller.signal })
        if (!response.ok) throw new Error('Não foi possível carregar os dados.')
        const data = await response.json() as T
        if (active) setResult({ key, data })
      } catch {
        if (active) {
          setResult({ key, data: null })
          reportError()
        }
      }
    }

    void load()
    return () => { active = false; controller.abort() }
  }, [url, key])

  return { data: result?.key === key ? result.data : null, loading: result?.key !== key }
}
