import type { Account, ActivityItem, Contact, Id, Task, TaskStatus, User } from './types'

function nowIso() {
    return new Date().toISOString()
}

function delay(ms: number) {
    return new Promise<void>((resolve) => setTimeout(resolve, ms))
}

function makeId(prefix: string) {
    return `${prefix}_${Math.random().toString(16).slice(2)}_${Date.now().toString(16)}`
}

const users: User[] = [
    { id: 'u_1', name: 'Ava Chen', email: 'ava@shalimar.local' },
    { id: 'u_2', name: 'Noah Patel', email: 'noah@shalimar.local' },
    { id: 'u_3', name: 'Mia Johnson', email: 'mia@shalimar.local' },
]

const accounts: Account[] = [
    { id: 'a_1', name: 'Northwind Traders', industry: 'Retail', tier: 'Mid', ownerId: 'u_1', updatedAt: nowIso() },
    { id: 'a_2', name: 'Contoso', industry: 'Software', tier: 'Enterprise', ownerId: 'u_2', updatedAt: nowIso() },
    { id: 'a_3', name: 'Fabrikam', industry: 'Manufacturing', tier: 'SMB', ownerId: 'u_3', updatedAt: nowIso() },
]

const contacts: Contact[] = [
    { id: 'c_1', accountId: 'a_1', name: 'Jordan Lee', title: 'Buyer', email: 'jordan@northwind.example' },
    { id: 'c_2', accountId: 'a_2', name: 'Riley Smith', title: 'VP Engineering', email: 'riley@contoso.example' },
    { id: 'c_3', accountId: 'a_2', name: 'Casey Nguyen', title: 'Procurement', email: 'casey@contoso.example' },
    { id: 'c_4', accountId: 'a_3', name: 'Taylor Kim', title: 'Owner', email: 'taylor@fabrikam.example' },
]

let tasks: Task[] = [
    {
        id: 't_1',
        title: 'Qualify inbound lead (Contoso)',
        status: 'todo',
        priority: 'high',
        accountId: 'a_2',
        assigneeId: 'u_1',
        dueAt: new Date(Date.now() + 1000 * 60 * 60 * 24).toISOString(),
        tags: ['lead', 'email'],
        updatedAt: nowIso(),
    },
    {
        id: 't_2',
        title: 'Prepare QBR deck (Northwind)',
        status: 'in_progress',
        priority: 'medium',
        accountId: 'a_1',
        assigneeId: 'u_2',
        dueAt: new Date(Date.now() + 1000 * 60 * 60 * 24 * 5).toISOString(),
        tags: ['qbr'],
        updatedAt: nowIso(),
    },
    {
        id: 't_3',
        title: 'Send renewal reminder (Fabrikam)',
        status: 'backlog',
        priority: 'low',
        accountId: 'a_3',
        assigneeId: 'u_3',
        tags: ['renewal'],
        updatedAt: nowIso(),
    },
    {
        id: 't_4',
        title: 'Resolve pricing blocker (Contoso)',
        status: 'blocked',
        priority: 'high',
        accountId: 'a_2',
        assigneeId: 'u_2',
        tags: ['pricing', 'legal'],
        updatedAt: nowIso(),
    },
    {
        id: 't_5',
        title: 'Post-call notes (Northwind)',
        status: 'done',
        priority: 'medium',
        accountId: 'a_1',
        assigneeId: 'u_1',
        tags: ['notes'],
        updatedAt: nowIso(),
    },
]

let activity: ActivityItem[] = [
    { id: 'act_1', ts: nowIso(), kind: 'system', summary: 'CRM agent bootstrapped with mock data.' },
]

function pushActivity(summary: string, kind: ActivityItem['kind'] = 'task') {
    activity = [{ id: makeId('act'), ts: nowIso(), kind, summary }, ...activity].slice(0, 50)
}

export async function getUsers(): Promise<User[]> {
    await delay(150)
    return users
}

export async function listAccounts(): Promise<Account[]> {
    await delay(250)
    return accounts.toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

export async function getAccount(id: Id): Promise<Account | undefined> {
    await delay(200)
    return accounts.find((a) => a.id === id)
}

export async function listContactsByAccount(accountId: Id): Promise<Contact[]> {
    await delay(220)
    return contacts.filter((c) => c.accountId === accountId)
}

export async function listTasks(): Promise<Task[]> {
    await delay(350)
    return tasks.toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

export async function getTask(id: Id): Promise<Task | undefined> {
    await delay(200)
    return tasks.find((t) => t.id === id)
}

export async function updateTaskStatus(id: Id, status: TaskStatus): Promise<Task | undefined> {
    await delay(180)
    const t = tasks.find((x) => x.id === id)
    if (!t) return undefined
    t.status = status
    t.updatedAt = nowIso()
    pushActivity(`Moved task "${t.title}" → ${status.replaceAll('_', ' ')}`)
    return t
}

export async function createTask(title: string, accountId?: Id): Promise<Task> {
    await delay(220)
    const t: Task = {
        id: makeId('t'),
        title,
        status: 'todo',
        priority: 'medium',
        accountId,
        tags: [],
        updatedAt: nowIso(),
    }
    tasks = [t, ...tasks]
    pushActivity(`Created task "${title}"`)
    return t
}

export async function listActivity(): Promise<ActivityItem[]> {
    await delay(120)
    return activity
}

