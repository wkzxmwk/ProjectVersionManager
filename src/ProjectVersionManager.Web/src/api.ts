import axios from 'axios'
import type {
  ApiResponse,
  LoginResponse,
  OperationLog,
  PagedResult,
  Personnel,
  ProgramGroupOption,
  ProjectOption,
  ProjectOptionForm,
  ProjectOptionImportResult,
  ProjectOptionType,
  ProjectVersion,
  ProjectVersionForm,
  ProjectVersionImportResult,
  ProjectVersionStatusCounts,
} from './types'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 20000,
})

api.interceptors.request.use((config) => {
  const token = sessionStorage.getItem('pvm_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      sessionStorage.removeItem('pvm_token')
      sessionStorage.removeItem('pvm_user')
      window.dispatchEvent(new Event('pvm:unauthorized'))
    }
    return Promise.reject(error)
  },
)

const unwrap = async <T>(request: Promise<{ data: ApiResponse<T> }>) => (await request).data.data

export const authApi = {
  login: (username: string, password: string) =>
    unwrap<LoginResponse>(api.post('/auth/login', { username, password })),
  changePassword: (currentPassword: string, newPassword: string) =>
    api.post('/auth/change-password', { currentPassword, newPassword }),
  logout: () => api.post('/auth/logout'),
}

export interface VersionQuery {
  page: number
  pageSize: number
  keyword?: string
  projectType?: string
  developer?: string
  compiler?: string
  programGroup?: string
  programName?: string
  staffGroup?: string
  personnelGroup?: string
  status?: string
  startDate?: string
  endDate?: string
  deletedOnly?: boolean
  excludeReleased?: boolean
}

export interface LogQuery {
  page: number
  pageSize: number
  keyword?: string
  operationType?: string
  startTime?: string
  endTime?: string
}

export const versionsApi = {
  list: (params: VersionQuery) => unwrap<PagedResult<ProjectVersion>>(api.get('/project-versions', { params })),
  statusCounts: () => unwrap<ProjectVersionStatusCounts>(api.get('/project-versions/status-counts')),
  get: (id: string) => unwrap<ProjectVersion>(api.get(`/project-versions/${id}`)),
  create: (payload: ProjectVersionForm) => unwrap<ProjectVersion>(api.post('/project-versions', payload)),
  update: (id: string, payload: ProjectVersionForm) => unwrap<ProjectVersion>(api.put(`/project-versions/${id}`, payload)),
  remove: (id: string, rowVersion: number) => api.delete(`/project-versions/${id}`, { params: { rowVersion } }),
  permanentRemove: (id: string, rowVersion: number) =>
    api.delete(`/project-versions/${id}/permanent`, { params: { rowVersion } }),
  batchDelete: (items: Array<{ id: string; rowVersion: number }>) =>
    api.post('/project-versions/batch-delete', { items }),
  batchRestore: (items: Array<{ id: string; rowVersion: number }>) =>
    api.post('/project-versions/batch-restore', { items }),
  export: (params: VersionQuery) =>
    api.get('/project-versions/export', { params, responseType: 'blob' }),
  downloadImportTemplate: () =>
    api.get('/project-versions/import-template', { responseType: 'blob' }),
  import: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return unwrap<ProjectVersionImportResult>(api.post('/project-versions/import', data))
  },
}

export const dictionariesApi = {
  projectTypes: () => unwrap<string[]>(api.get('/dictionaries/project-types')),
  developers: () => unwrap<string[]>(api.get('/dictionaries/developers')),
  staffGroups: () => unwrap<string[]>(api.get('/dictionaries/staff-groups')),
  personnelGroups: (staffGroup?: string) =>
    unwrap<string[]>(api.get('/dictionaries/personnel-groups', { params: { staffGroup } })),
  programGroups: (personnelGroup?: string) =>
    unwrap<string[]>(api.get('/dictionaries/program-groups', { params: { personnelGroup } })),
  programGroupOptions: (personnelGroup?: string) =>
    unwrap<ProgramGroupOption[]>(api.get('/dictionaries/program-group-options', { params: { personnelGroup } })),
  programNames: (programGroup?: string, personnelGroup?: string) =>
    unwrap<string[]>(api.get('/dictionaries/program-names', { params: { programGroup, personnelGroup } })),
  compilers: () => unwrap<string[]>(api.get('/dictionaries/compilers')),
}

export const projectOptionsApi = {
  list: (optionType?: ProjectOptionType) =>
    unwrap<ProjectOption[]>(api.get('/project-options', { params: { optionType } })),
  create: (payload: ProjectOptionForm) =>
    unwrap<ProjectOption>(api.post('/project-options', payload)),
  update: (id: string, payload: ProjectOptionForm) =>
    unwrap<ProjectOption>(api.put(`/project-options/${id}`, payload)),
  remove: (id: string, rowVersion: number) =>
    api.delete(`/project-options/${id}`, { params: { rowVersion } }),
  import: (file: File) => {
    const data = new FormData()
    data.append('file', file)
    return unwrap<ProjectOptionImportResult>(api.post('/project-options/import', data))
  },
  downloadImportTemplate: (optionType?: string) =>
    api.get('/project-options/import-template', { params: { optionType }, responseType: 'blob' }),
}

export const personnelApi = {
  list: () => unwrap<Personnel[]>(api.get('/personnel')),
  create: (payload: { employeeNumber: string; personnelName: string; managementPassword: string; staffGroup?: string; personnelGroup?: string }) =>
    unwrap<Personnel>(api.post('/personnel', payload)),
  update: (id: string, payload: { employeeNumber: string; personnelName: string; staffGroup?: string; personnelGroup?: string }) =>
    unwrap<Personnel>(api.put(`/personnel/${id}`, payload)),
  remove: (id: string) => api.delete(`/personnel/${id}`),
  resetPassword: (id: string, managementPassword: string) =>
    api.post(`/personnel/${id}/reset-password`, { managementPassword }),
  updatePermissions: (id: string, permissions: string[]) =>
    unwrap<Personnel>(api.put(`/personnel/${id}/permissions`, { permissions })),
}

export const logsApi = {
  list: (params: LogQuery) => unwrap<PagedResult<OperationLog>>(api.get('/operation-logs', { params })),
}

export const getErrorMessage = (error: unknown, fallback = '请求失败，请稍后重试') => {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data
    if (data?.message || data?.detail) {
      const message = data.message || data.detail
      return data.traceId ? `${message}（跟踪号：${data.traceId}）` : message
    }
    if (data?.errors && typeof data.errors === 'object') {
      const messages = Object.values(data.errors)
        .flatMap((value) => Array.isArray(value) ? value : [value])
        .filter((value): value is string => typeof value === 'string')
      if (messages.length) return messages.join('；')
    }
  }
  return fallback
}
