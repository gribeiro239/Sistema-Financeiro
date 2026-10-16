export const workspaceStorageKey = 'nexo-workspace-id'

export function apiFetch(input: RequestInfo | URL, init: RequestInit = {}) {
  const headers = new Headers(init.headers ?? (input instanceof Request ? input.headers : undefined))
  const workspaceId = window.localStorage.getItem(workspaceStorageKey)
  if (workspaceId) headers.set('X-Workspace-Id', workspaceId)
  return window.fetch(input, { ...init, headers }).then(response => {
    const target = input instanceof Request ? input.url : input.toString()
    if (response.status === 401 && !target.includes('/api/auth/')) {
      window.dispatchEvent(new Event('nexo:unauthorized'))
    }
    return response
  })
}
