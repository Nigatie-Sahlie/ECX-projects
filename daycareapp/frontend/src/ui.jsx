import { useCallback, useEffect, useState } from 'react'

// Loads data on mount; returns [data, reload, error]
export function useLoad(fn, deps = []) {
  const [data, setData] = useState(null)
  const [err, setErr] = useState('')
  // eslint-disable-next-line react-hooks/exhaustive-deps
  const reload = useCallback(() => fn().then((d) => { setData(d); setErr('') }).catch((e) => setErr(e.message)), deps)
  useEffect(() => { reload() }, [reload])
  return [data, reload, err]
}

export const Notice = ({ children, kind = 'error' }) =>
  children ? <p className={`notice ${kind}`} role="alert">{children}</p> : null

export const Empty = ({ children }) => <p className="empty">{children}</p>

export function Field({ label, children }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  )
}

// Run an async action, show errors, then refresh
export function useAction() {
  const [err, setErr] = useState('')
  const run = async (fn) => {
    setErr('')
    try { await fn(); return true } catch (e) { setErr(e.message); return false }
  }
  return [run, err]
}

export function ClassroomPicker({ classrooms, value, onChange }) {
  const toggle = (id) => onChange(value.includes(id) ? value.filter((x) => x !== id) : [...value, id])
  if (!classrooms?.length) return <span className="muted">Add a classroom first.</span>
  return (
    <div className="chips">
      {classrooms.map((c) => (
        <label key={c.id} className={`chip ${value.includes(c.id) ? 'on' : ''}`}>
          <input type="checkbox" checked={value.includes(c.id)} onChange={() => toggle(c.id)} />
          {c.name}
        </label>
      ))}
    </div>
  )
}
