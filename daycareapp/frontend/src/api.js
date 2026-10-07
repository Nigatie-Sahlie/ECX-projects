const BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000'

export async function api(path, { method = 'GET', body } = {}) {
  const token = localStorage.getItem('token')
  const res = await fetch(`${BASE}/api${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })
  if (res.status === 401 && token) {
    // session expired
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    window.location.href = '/login'
  }
  if (!res.ok) {
    let msg = 'Something went wrong. Try again.'
    try {
      const j = await res.json()
      msg = j.error || j.title || msg
    } catch {}
    throw new Error(msg)
  }
  return res.status === 204 ? null : res.json()
}

// local calendar date as YYYY-MM-DD
export const today = () => new Date().toLocaleDateString('en-CA')

export const fmtTime = (iso) =>
  iso ? new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''

export const fmtDate = (d) =>
  new Date(d + (d.length === 10 ? 'T00:00:00' : '')).toLocaleDateString([], {
    weekday: 'short', day: 'numeric', month: 'short', year: 'numeric',
  })

export const age = (dob) => {
  const b = new Date(dob)
  const months = (new Date().getFullYear() - b.getFullYear()) * 12 + new Date().getMonth() - b.getMonth()
  return months < 24 ? `${months} months` : `${Math.floor(months / 12)} years`
}
