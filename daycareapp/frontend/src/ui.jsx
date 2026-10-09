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

export function SearchBox({ value, onChange, placeholder = 'Search…' }) {
  return <Field label="Search"><input type="search" value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} /></Field>
}

export function Pagination({ page, pageSize, total, onPageChange }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  if (total <= pageSize) return null
  return (
    <div className="pagination" aria-label="Pagination">
      <button className="btn ghost" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Previous</button>
      <span className="muted">Page {page} of {pages} · {total} results</span>
      <button className="btn ghost" disabled={page >= pages} onClick={() => onPageChange(page + 1)}>Next</button>
    </div>
  )
}

export function Field({ label, children }) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
    </label>
  )
}

export function PasswordField({ label, value, onChange, required = false, minLength, autoComplete }) {
  const [visible, setVisible] = useState(false)
  return (
    <Field label={label}>
      <div className="password-input">
        <input
          required={required}
          minLength={minLength}
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={onChange}
          autoComplete={autoComplete}
        />
        <button
          type="button"
          className="password-toggle"
          aria-label={visible ? 'Hide password' : 'Show password'}
          title={visible ? 'Hide password' : 'Show password'}
          onClick={() => setVisible(!visible)}
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            {visible
              ? <><path d="M3 3l18 18" /><path d="M10.6 10.6a2 2 0 0 0 2.8 2.8" /><path d="M9.9 5.2A10.8 10.8 0 0 1 12 5c5 0 8.5 4 9.5 7a11.7 11.7 0 0 1-2.1 3.7" /><path d="M6.2 6.2C3.9 7.8 2.8 10.1 2.5 12c.5 2 3.4 6 9.5 6 1.2 0 2.3-.2 3.2-.6" /></>
              : <><path d="M2.5 12S6 5 12 5s9.5 7 9.5 7S18 19 12 19 2.5 12 2.5 12Z" /><circle cx="12" cy="12" r="2.5" /></>}
          </svg>
        </button>
      </div>
    </Field>
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
