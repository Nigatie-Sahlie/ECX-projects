import { api, fmtDate } from '../api'
import { Notice, useLoad } from '../ui.jsx'
import { useAuth } from '../App.jsx'

export default function Dashboard() {
  const { user } = useAuth()
  const isSuperAdmin = user.role === 'super_admin'
  const [data, , err] = useLoad(() => api('/dashboard'))
  if (!data) return <><h1>Dashboard</h1><Notice>{err}</Notice></>
  if (isSuperAdmin) {
    return (
      <>
        <h1>Platform dashboard</h1><Notice>{err}</Notice>
        <section className="dashboard-cards">
          {[['Children', data.children], ['Staff', data.staff], ['Daycares', data.centers], ['Active daycares', data.activeCenters]].map(([label, value]) => <div className="panel metric" key={label}><span>{label}</span><strong>{value}</strong></div>)}
        </section>
      </>
    )
  }
  const cards = [
    ['Present now', data.presentNow ?? data.checkedIn ?? 0],
    ['Enrolled', data.enrolled ?? data.activeChildren ?? data.children ?? 0],
    ['Checked out today', data.checkedOutToday ?? 0],
    ['Reports not shared today', data.reportsNotSharedToday ?? 0],
  ]
  return (
    <>
      <h1>Dashboard</h1><Notice>{err}</Notice>
      <section className="dashboard-cards">{cards.map(([label, value]) => <div className="panel metric" key={label}><span>{label}</span><strong>{value}</strong></div>)}</section>
      <section className="panel">
        <h2>Recent announcements</h2>
        {Array.isArray(data.announcements) && data.announcements.length > 0
          ? data.announcements.map((a) => <div className="card" key={a.id}><strong>{a.title}</strong><div className="muted">{a.author} · {a.publishedAt ? fmtDate(new Date(a.publishedAt).toLocaleDateString('en-CA')) : ''}</div><p className="pre">{a.body}</p></div>)
          : <p className="muted">{data.announcements ? `${data.announcements} announcement${data.announcements === 1 ? '' : 's'} published.` : 'No recent announcements.'}</p>}
      </section>
    </>
  )
}
