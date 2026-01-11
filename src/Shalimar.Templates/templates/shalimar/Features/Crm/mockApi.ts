import type { Account, ActivityItem, Contact, Epic, Id, Task, TaskActor, TaskArtifact, TaskDecision, TaskMessage, TaskStatus, User } from './types'
import { mutateCrmTasks } from '@generated/shalimar-mutations.g'
import type { CreateTaskRequest, TaskDto } from '@generated/shalimar-types.g'

// Shalimar principle: server is truth. This module calls feature endpoints (no /api).

type CrmSnapshotDto = {
    users: User[]
    accounts: Account[]
    epics: Epic[]
    tasks: Task[]
    activity: ActivityItem[]
}

let snapshotCache: CrmSnapshotDto | null = null
let snapshotInFlight: Promise<CrmSnapshotDto> | null = null

async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
    const res = await fetch(url, {
        ...init,
        headers: {
            'content-type': 'application/json',
            ...(init?.headers ?? {}),
        },
    })
    if (!res.ok) {
        const text = await res.text().catch(() => '')
        throw new Error(`Request failed (${res.status}) ${url}\n${text}`)
    }
    return (await res.json()) as T
}

async function getSnapshot(): Promise<CrmSnapshotDto> {
    if (snapshotCache) return snapshotCache
    if (snapshotInFlight) return snapshotInFlight
    snapshotInFlight = getJson<CrmSnapshotDto>('/crm/snapshot').then((s) => {
        snapshotCache = s
        snapshotInFlight = null
        return s
    })
    return snapshotInFlight
}

function invalidateSnapshot() {
    snapshotCache = null
    snapshotInFlight = null
}

export async function getUsers(): Promise<User[]> {
    const s = await getSnapshot()
    return s.users
}

export async function listAccounts(): Promise<Account[]> {
    const s = await getSnapshot()
    return s.accounts.toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

export async function getAccount(id: Id): Promise<Account | undefined> {
    const s = await getSnapshot()
    return s.accounts.find((a) => a.id === id)
}

export async function listContactsByAccount(accountId: Id): Promise<Contact[]> {
    return await getJson<Contact[]>(`/crm/accounts/${accountId}/contacts`)
}

export async function listTasks(): Promise<Task[]> {
    const s = await getSnapshot()
    return s.tasks.toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

export async function listEpics(): Promise<Epic[]> {
    const s = await getSnapshot()
    return s.epics.toSorted((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

export async function getEpic(id: Id): Promise<Epic | undefined> {
    const s = await getSnapshot()
    return s.epics.find((e) => e.id === id)
}

export async function getTask(id: Id): Promise<Task | undefined> {
    const s = await getSnapshot()
    return s.tasks.find((t) => t.id === id)
}

export async function updateTaskStatus(id: Id, status: TaskStatus): Promise<Task | undefined> {
    return await moveTask(id, status)
}

export async function moveTask(id: Id, status: TaskStatus, epicId?: Id): Promise<Task | undefined> {
    invalidateSnapshot()
    return await getJson<Task>(`/crm/tasks/${id}/move`, {
        method: 'POST',
        body: JSON.stringify({
            status,
            epicIdSet: true,
            epicId: epicId ?? null,
        }),
    })
}

export async function updateTask(
    id: Id,
    patch: Partial<
        Pick<Task, 'title' | 'priority' | 'assigneeId' | 'accountId' | 'dueAt' | 'estimateMinutes' | 'collaboratorIds' | 'epicId'>
    >,
): Promise<Task | undefined> {
    invalidateSnapshot()
    return await getJson<Task>(`/crm/tasks/${id}`, {
        method: 'PATCH',
        body: JSON.stringify({
            title: patch.title,
            priority: patch.priority,
            epicIdSet: 'epicId' in patch,
            epicId: patch.epicId ?? null,
            accountIdSet: 'accountId' in patch,
            accountId: patch.accountId ?? null,
            assigneeIdSet: 'assigneeId' in patch,
            assigneeId: patch.assigneeId ?? null,
            dueAtSet: 'dueAt' in patch,
            dueAt: patch.dueAt ?? null,
            estimateMinutesSet: 'estimateMinutes' in patch,
            estimateMinutes: patch.estimateMinutes ?? null,
            collaboratorIds: patch.collaboratorIds ?? null,
        }),
    })
}

export async function createTask(title: string, accountId?: Id): Promise<Task> {
    invalidateSnapshot()

    const req: CreateTaskRequest = {
        title,
        priority: 'medium',
        epicId: null,
        accountId: accountId ?? null,
        assigneeId: null,
        collaboratorIds: [],
        dueAt: null,
        estimateMinutes: null,
        tags: [],
    }

    const dto = await mutateCrmTasks(req)
    return toTask(dto)
}

function toTask(dto: TaskDto): Task {
    return {
        id: dto.id,
        title: dto.title,
        status: coerceTaskStatus(dto.status),
        priority: coerceTaskPriority(dto.priority),
        epicId: dto.epicId ?? undefined,
        accountId: dto.accountId ?? undefined,
        assigneeId: dto.assigneeId ?? undefined,
        collaboratorIds: dto.collaboratorIds,
        dueAt: dto.dueAt ?? undefined,
        estimateMinutes: dto.estimateMinutes ?? undefined,
        tags: dto.tags,
        updatedAt: dto.updatedAt,
    }
}

function coerceTaskStatus(v: string): TaskStatus {
    switch (v) {
        case 'backlog':
        case 'todo':
        case 'in_progress':
        case 'blocked':
        case 'done':
            return v
        default:
            return 'todo'
    }
}

function coerceTaskPriority(v: string): Task['priority'] {
    switch (v) {
        case 'low':
        case 'medium':
        case 'high':
            return v
        default:
            return 'medium'
    }
}

export async function listTaskMessages(taskId: Id): Promise<TaskMessage[]> {
    return await getJson<TaskMessage[]>(`/crm/tasks/${taskId}/messages`)
}

export async function listTaskArtifacts(taskId: Id): Promise<TaskArtifact[]> {
    return await getJson<TaskArtifact[]>(`/crm/tasks/${taskId}/artifacts`)
}

export async function listTaskDecisions(taskId: Id): Promise<TaskDecision[]> {
    return await getJson<TaskDecision[]>(`/crm/tasks/${taskId}/decisions`)
}

export async function addTaskArtifact(
    taskId: Id,
    actor: TaskActor,
    kind: TaskArtifact['kind'],
    title: string,
    content: string,
): Promise<TaskArtifact> {
    invalidateSnapshot()
    return await getJson<TaskArtifact>(`/crm/tasks/${taskId}/artifacts`, {
        method: 'POST',
        body: JSON.stringify({ kind, title, content, createdBy: actor }),
    })
}

export async function addTaskDecision(
    taskId: Id,
    actor: TaskActor,
    question: string,
    options: string[],
    outcome: string,
    rationale?: string,
): Promise<TaskDecision> {
    invalidateSnapshot()
    return await getJson<TaskDecision>(`/crm/tasks/${taskId}/decisions`, {
        method: 'POST',
        body: JSON.stringify({ question, options, outcome, rationale: rationale ?? null, madeBy: actor }),
    })
}

export async function postTaskMessage(taskId: Id, actor: TaskActor, body: string): Promise<TaskMessage> {
    invalidateSnapshot()
    return await getJson<TaskMessage>(`/crm/tasks/${taskId}/messages`, {
        method: 'POST',
        body: JSON.stringify({ actor, body }),
    })
}

export async function listActivity(): Promise<ActivityItem[]> {
    const s = await getSnapshot()
    return s.activity
}

