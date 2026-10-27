export interface ApiResponse<T> {
  success: boolean
  message: string
  data: T
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
  totalPages: number
}

export interface LoginResponse {
  token: string
  expiresAt: string
  username: string
  displayName: string
  roleCode: string
  staffGroup?: string
  personnelGroup?: string
  permissions: string[]
}

export interface ProjectVersion {
  id: string
  projectDate: string
  projectType: string
  developer: string
  programGroup: string
  personnelGroup?: string
  programName: string
  programVersion: string
  compiler?: string
  compileTime?: string
  compiledProgramVersion?: string
  versionDescription?: string
  releaseTime?: string
  branchName?: string
  remark1?: string
  remark2?: string
  status: 'PENDING' | 'WAIT_COMPILE' | 'COMPILED' | 'PRE_RELEASED' | 'RELEASED' | 'CANCELED' | 'COMMENTED'
  createdBy: string
  createdAt: string
  updatedBy: string
  updatedAt: string
  isDeleted: boolean
  rowVersion: number
}

export interface ProjectVersionForm {
  projectDate: string
  projectType: string
  developer: string
  programGroup: string
  personnelGroup?: string
  programName: string
  programVersion: string
  compiler?: string
  compileTime?: string
  compiledProgramVersion?: string
  versionDescription?: string
  releaseTime?: string
  branchName?: string
  remark1?: string
  remark2?: string
  status: 'PENDING' | 'WAIT_COMPILE' | 'COMPILED' | 'PRE_RELEASED' | 'RELEASED' | 'CANCELED' | 'COMMENTED'
  rowVersion: number
}

export interface ProjectVersionStatusCounts {
  total: number
  pending: number
  waitCompile: number
  compiled: number
  preReleased: number
  released: number
  commented: number
}

export interface ProjectVersionImportResult {
  totalRows: number
  createdCount: number
  skippedDuplicateCount: number
}

export interface Personnel {
  id: string
  employeeNumber: string
  personnelName: string
  staffGroup?: string
  personnelGroup?: string
  createdAt: string
  updatedAt: string
  permissions: string[]
}

export interface OperationLog {
  id: string
  businessId: string
  operationType: string
  operator: string
  operationTime: string
  programName?: string
  programVersion?: string
  beforeData?: string
  afterData?: string
  ipAddress?: string
  userAgent?: string
}

export type ProjectOptionType =
  | 'PROJECT_TYPE'
  | 'STAFF_GROUP'
  | 'PERSONNEL_GROUP'
  | 'DEVELOPER'
  | 'PROGRAM_GROUP'
  | 'PROGRAM_NAME'
  | 'COMPILER'

export interface ProjectOption {
  id: string
  optionType: ProjectOptionType
  optionValue: string
  staffGroup?: string
  personnelGroup?: string
  programGroup?: string
  sortOrder: number
  updatedBy: string
  updatedAt: string
  rowVersion: number
}

export interface ProjectOptionForm {
  optionType: ProjectOptionType
  optionValue: string
  staffGroup?: string
  personnelGroup?: string
  programGroup?: string
  sortOrder: number
  rowVersion: number
}

export interface ProjectOptionImportResult {
  totalRows: number
  createdCount: number
  skippedDuplicateCount: number
}

export interface ProgramGroupOption {
  optionValue: string
  personnelGroup: string
  staffGroup: string
}
