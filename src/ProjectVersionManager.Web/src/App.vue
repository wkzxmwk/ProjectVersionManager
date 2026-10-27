<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import {
  ArrowDown,
  CircleCheck,
  Clock,
  Collection,
  Document,
  Download,
  EditPen,
  Lock,
  Plus,
  Refresh,
  RefreshLeft,
  Search,
  Setting,
  Tickets,
  Upload,
  User,
  View,
  Warning,
  Delete,
} from '@element-plus/icons-vue'
import {
  authApi,
  dictionariesApi,
  getErrorMessage,
  logsApi,
  personnelApi,
  projectOptionsApi,
  versionsApi,
  type LogQuery,
  type VersionQuery,
} from './api'
import type {
  LoginResponse,
  OperationLog,
  Personnel,
  ProgramGroupOption,
  ProjectOption,
  ProjectOptionForm,
  ProjectOptionType,
  ProjectVersion,
  ProjectVersionForm,
  ProjectVersionStatusCounts,
} from './types'

const loginForm = reactive({ username: '', password: '' })
const loginLoading = ref(false)
const loginError = ref('')
const passwordInputRef = ref<InstanceType<typeof import('element-plus')['ElInput']>>()
function focusPassword() {
  passwordInputRef.value?.focus()
}
const changePasswordDialogVisible = ref(false)
const changePasswordLoading = ref(false)
const changePasswordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})
const sidebarCollapsed = ref(false)
function toggleSidebar() { sidebarCollapsed.value = !sidebarCollapsed.value }
const token = ref(sessionStorage.getItem('pvm_token') || '')
const user = ref<LoginResponse | null>(JSON.parse(sessionStorage.getItem('pvm_user') || 'null'))
const activeView = ref<'records' | 'logs' | 'settings' | 'personnel' | 'no-access'>('records')
const pageLoading = ref(false)
const records = ref<ProjectVersion[]>([])
const statusCounts = reactive<ProjectVersionStatusCounts>({
  total: 0,
  pending: 0,
  waitCompile: 0,
  compiled: 0,
  preReleased: 0,
  released: 0,
  commented: 0,
})
const logs = ref<OperationLog[]>([])
const personnel = ref<Personnel[]>([])
const personnelLoading = ref(false)
const personnelDialogVisible = ref(false)
const personnelDialogMode = ref<'create' | 'edit'>('create')
const personnelSaving = ref(false)
const personnelFormRef = ref<FormInstance>()
const personnelForm = reactive({
  id: '',
  employeeNumber: '',
  personnelName: '',
  managementPassword: '',
  staffGroup: '',
  personnelGroup: '',
})
const resetPasswordDialogVisible = ref(false)
const resetPasswordLoading = ref(false)
const resetPasswordId = ref('')
const resetPasswordName = ref('')
const resetPassword = ref('')
const permissionDialogVisible = ref(false)
const permissionSaving = ref(false)
const permissionPersonnelId = ref('')
const permissionPersonnelName = ref('')
const selectedPermissions = ref<string[]>([])
const total = ref(0)
const logTotal = ref(0)
const currentPage = ref(1)
const logPage = ref(1)
const pageSize = ref(20)
const logPageSize = ref(20)
const formRef = ref<FormInstance>()
const drawerVisible = ref(false)
const recordSaving = ref(false)
const detailVisible = ref(false)
const editingId = ref<string | null>(null)
const detailRecord = ref<ProjectVersion | null>(null)
const selectedRecords = ref<ProjectVersion[]>([])
const showDeleted = ref(false)
const versionImportDialogVisible = ref(false)
const versionImportLoading = ref(false)
const versionTemplateLoading = ref(false)
const exportLoading = ref(false)
const versionImportFile = ref<File | null>(null)
const versionImportInputKey = ref(0)
const projectOptions = ref<ProjectOption[]>([])
const optionLoading = ref(false)
const optionDialogVisible = ref(false)
const optionImportDialogVisible = ref(false)
const optionImportLoading = ref(false)
const optionImportFile = ref<File | null>(null)
const optionImportInputKey = ref(0)
const optionTemplateLoading = ref(false)
const editingOptionId = ref<string | null>(null)
const optionFormRef = ref<FormInstance>()
const selectedOptionType = ref<ProjectOptionType>('PROJECT_TYPE')
const optionStaffGroupFilter = ref<string>('')
const optionPersonnelGroupFilter = ref<string>('')
const optionProgramGroupFilter = ref<string>('')
const optionKeywordFilter = ref<string>('')
const selectedProjectOptions = ref<ProjectOption[]>([])

const filters = reactive({
  keyword: '',
  projectType: '',
  developer: '',
  compiler: '',
  programGroup: '',
  programName: '',
  staffGroup: '',
  personnelGroup: '',
  status: 'UNRELEASED',
  startDate: '',
  endDate: '',
})
const logFilters = reactive({
  keyword: '',
  operationType: '',
  startTime: '',
  endTime: '',
})
const options = reactive({
  projectTypes: [] as string[],
  developers: [] as string[],
  staffGroups: [] as string[],
  personnelGroups: [] as string[],
  programGroups: [] as string[],
  programGroupOptions: [] as ProgramGroupOption[],
  programNames: [] as string[],
  compilers: [] as string[],
})
const optionTypeDefinitions: Array<{
  type: ProjectOptionType
  label: string
  description: string
}> = [
  { type: 'PROJECT_TYPE', label: '项目类型', description: '例如 SR、CR、需求或缺陷' },
  { type: 'STAFF_GROUP', label: '项目组', description: '维护人员所属的分组' },
  { type: 'PERSONNEL_GROUP', label: '人员组', description: '项目组下的人员分组' },
  { type: 'PROGRAM_GROUP', label: '专案名称', description: '维护项目或程序所属分组' },
  { type: 'PROGRAM_NAME', label: '程序名', description: '维护程序名称及其所属专案' },
]
const permissionDefinitions = [
  { code: 'VERSION_VIEW', label: '查看项目版本', description: '查看、筛选、导出版本记录及下载模板' },
  { code: 'VERSION_CREATE', label: '新增版本记录', description: '新增单条项目版本记录' },
  { code: 'VERSION_UPDATE', label: '修改版本记录', description: '编辑现有项目版本记录' },
  { code: 'VERSION_DELETE', label: '删除与恢复版本', description: '删除、批量删除及从回收站恢复' },
  { code: 'VERSION_IMPORT', label: '批量导入版本', description: '通过 Excel 批量导入项目版本' },
  { code: 'LOG_VIEW', label: '查看操作日志', description: '查看项目版本操作审计记录' },
  { code: 'OPTION_MANAGE', label: '管理项目设置', description: '新增、修改、删除和导入项目选项' },
  { code: 'PROGRAM_RELATION_MANAGE', label: '管理项目组与程序名', description: '维护项目组、程序名及两者关联关系' },
  { code: 'PERSONNEL_MANAGE', label: '管理人员账号', description: '维护人员账号和重置密码；权限分配仅限管理员' },
]
const optionForm = reactive<ProjectOptionForm>({
  optionType: 'PROJECT_TYPE',
  optionValue: '',
  staffGroup: '',
  personnelGroup: '',
  programGroup: '',
  sortOrder: 10,
  rowVersion: 0,
})

const form = reactive<ProjectVersionForm>({
  projectDate: '',
  projectType: '',
  developer: '',
  programGroup: '',
  personnelGroup: '',
  programName: '',
  programVersion: '',
  compiler: '',
  compileTime: '',
  compiledProgramVersion: '',
  versionDescription: '',
  releaseTime: '',
  branchName: '',
  remark1: '',
  remark2: '',
  status: 'WAIT_COMPILE',
  rowVersion: 0,
})

// 复合键：`${optionValue}\u0001${personnelGroup}`，用于区分不同人员组下同名专案
const formProgramGroupKey = ref('')

const formRules: FormRules<ProjectVersionForm> = {
  projectDate: [{ required: true, message: '请选择项目时间', trigger: 'change' }],
  projectType: [{ required: true, message: '请选择项目类型', trigger: 'change' }],
  developer: [{ required: true, message: '请选择开发人', trigger: 'change' }],
  programGroup: [{ required: true, message: '请选择专案名称', trigger: 'change' }],
  programName: [{ required: true, message: '请选择程序名', trigger: 'change' }],
  programVersion: [{ required: true, message: '请输入程序版本', trigger: 'blur' }],
}
const optionFormRules: FormRules<ProjectOptionForm> = {
  optionType: [{ required: true, message: '请选择选项分类', trigger: 'change' }],
  optionValue: [
    { required: true, message: '请输入选项内容', trigger: 'blur' },
    { max: 200, message: '选项内容不能超过 200 个字符', trigger: 'blur' },
  ],
  programGroup: [{ required: true, message: '请选择程序名所属项目组', trigger: 'change' }],
  sortOrder: [{ required: true, message: '请输入排序值', trigger: 'change' }],
}
const personnelFormRules: FormRules<typeof personnelForm> = {
  employeeNumber: [
    { required: true, message: '请输入操作人员工号', trigger: 'blur' },
    { pattern: /^[A-Za-z0-9._-]+$/, message: '工号只能包含字母、数字、点、下划线和短横线', trigger: 'blur' },
  ],
  personnelName: [{ required: true, message: '请输入人员姓名', trigger: 'blur' }],
  personnelGroup: [{ required: true, message: '请选择所属人员组', trigger: 'change' }],
  managementPassword: [
    {
      validator: (_rule, value, callback) => {
        if (personnelDialogMode.value === 'create' && !value) {
          callback(new Error('请输入管理密码'))
        } else if (value && value.length < 6) {
          callback(new Error('管理密码至少 6 位'))
        } else {
          callback()
        }
      },
      trigger: 'blur',
    },
  ],
}

const isLoggedIn = computed(() => Boolean(token.value))
const isAdmin = computed(() => user.value?.roleCode === 'ADMIN')
const hasPermission = (permission: string) =>
  isAdmin.value || Boolean(user.value?.permissions?.includes(permission))
const displayName = computed(() => user.value?.displayName || user.value?.username || '访客')
const currentStaffGroup = computed(() => user.value?.staffGroup || '')
const filteredDevelopers = computed(() => {
  if (!isAdmin.value) return options.developers
  if (!filters.staffGroup) return options.developers
  const sg = filters.staffGroup
  return personnel.value
    .filter(item => (item.staffGroup || '') === sg)
    .map(item => item.personnelName)
})
const filteredCompilers = computed(() => {
  if (!isAdmin.value) return options.compilers
  if (!filters.staffGroup) return options.compilers
  const sg = filters.staffGroup
  return personnel.value
    .filter(item => (item.staffGroup || '') === sg)
    .map(item => item.personnelName)
})
const filteredProgramGroups = computed(() => {
  // 版本记录页面可能还没访问项目设置，projectOptions 可能为空；
  // 此时优先使用 loadDictionaries() 加载的 options.programGroupOptions（按后端权限范围过滤），
  // projectOptions 可用时则用其精确过滤（含 staffGroup / personnelGroup 信息）。
  if (!isAdmin.value) {
    const sg = currentStaffGroup.value
    const pg = user.value?.personnelGroup || ''
    if (sg && pg && sg === pg) {
      const fromOptions = projectOptions.value.length > 0
        ? Array.from(new Set(projectOptions.value
            .filter(item => item.optionType === 'PROGRAM_GROUP' && (item.staffGroup || '') === sg)
            .map(item => item.optionValue)))
        : []
      if (fromOptions.length > 0) return fromOptions
      return Array.from(new Set(options.programGroupOptions
        .filter(item => (item.staffGroup || '') === sg)
        .map(item => item.optionValue)))
    }
    if (pg) {
      const fromOptions = projectOptions.value.length > 0
        ? Array.from(new Set(projectOptions.value
            .filter(item => item.optionType === 'PROGRAM_GROUP' && (item.personnelGroup || '') === pg)
            .map(item => item.optionValue)))
        : []
      if (fromOptions.length > 0) return fromOptions
      return Array.from(new Set(options.programGroupOptions
        .filter(item => (item.personnelGroup || '') === pg)
        .map(item => item.optionValue)))
    }
    return options.programGroups
  }
  if (!filters.staffGroup && !filters.personnelGroup) return options.programGroups
  const fromOptions = projectOptions.value
    .filter(item => item.optionType === 'PROGRAM_GROUP')
    .filter(item => {
      if (!filters.staffGroup) return true
      return (item.staffGroup || '') === filters.staffGroup
    })
    .filter(item => {
      if (!filters.personnelGroup) return true
      return (item.personnelGroup || '') === filters.personnelGroup
    })
    .map(item => item.optionValue)
  if (fromOptions.length > 0) return fromOptions
  // admin + 字典回退
  let fallback = options.programGroupOptions
  if (filters.staffGroup) fallback = fallback.filter(item => (item.staffGroup || '') === filters.staffGroup)
  if (filters.personnelGroup) fallback = fallback.filter(item => (item.personnelGroup || '') === filters.personnelGroup)
  return Array.from(new Set(fallback.map(item => item.optionValue)))
})
const filteredProgramNames = computed(() => {
  const programNameOptions = projectOptions.value.filter(item => item.optionType === 'PROGRAM_NAME')
  if (programNameOptions.length === 0) return options.programNames

  return Array.from(new Set(programNameOptions
    .filter(item => !filters.programGroup || (item.programGroup || '') === filters.programGroup)
    .filter(item => !filters.staffGroup || (item.staffGroup || '') === filters.staffGroup)
    .filter(item => !filters.personnelGroup || (item.personnelGroup || '') === filters.personnelGroup)
    .map(item => item.optionValue)))
})
// 新增/编辑版本表单中专案名称下拉框的选项：专案名称与人员组合并显示，便于区分同名专案
interface FormProgramGroupItem {
  value: string
  label: string
  optionValue: string
  personnelGroup: string
  staffGroup: string
}
// 根据当前选中的开发人，从人员管理中查询其所属项目组/人员组
const formDeveloperRecord = computed<Personnel | undefined>(() => {
  const devName = (form.developer || '').trim()
  if (!devName) return undefined
  const found = personnel.value.find(item =>
    (item.personnelName || '').trim().toLowerCase() === devName.toLowerCase())
  if (found) return found
  // 普通用户可能没有加载人员列表，回退使用当前用户信息
  if (!isAdmin.value) {
    const me = user.value
    if (me && (me.displayName || me.username)?.toLowerCase() === devName.toLowerCase()) {
      return {
        id: '',
        employeeNumber: '',
        personnelName: me.displayName || me.username,
        staffGroup: me.staffGroup,
        personnelGroup: me.personnelGroup,
        createdAt: '',
        updatedAt: '',
        permissions: [],
      }
    }
  }
  return undefined
})
const formProgramGroupOptions = computed<FormProgramGroupItem[]>(() => {
  // 字典接口已按当前用户项目组过滤；表单中展示所有可访问的专案，不受列表筛选条件影响
  const base = options.programGroupOptions
  const dev = formDeveloperRecord.value
  const seen = new Set<string>()
  const result: FormProgramGroupItem[] = []
  for (const item of base) {
    const optionValue = item.optionValue
    const personnelGroup = item.personnelGroup || ''
    const staffGroup = item.staffGroup || ''
    const key = `${optionValue}\u0001${personnelGroup}\u0001${staffGroup}`
    if (seen.has(key)) continue
    // 基于开发人过滤：选了开发人后，仅展示该开发人项目组下的专案
    if (dev) {
      const devStaffGroup = (dev.staffGroup || '').trim()
      const devPersonnelGroup = (dev.personnelGroup || '').trim()
      if (devStaffGroup) {
        if (staffGroup && staffGroup.toLowerCase() !== devStaffGroup.toLowerCase()) continue
        // 若开发人人员组名称与项目组名称相同，显示此项目组下所有专案（不受人员组限制）
        const unrestricted =
          devPersonnelGroup &&
          devPersonnelGroup.localeCompare(devStaffGroup, undefined, { sensitivity: 'base' }) === 0
        if (!unrestricted && devPersonnelGroup) {
          // 普通受限制场景：仅显示相同项目组 + 相同人员组下的专案
          if (personnelGroup && personnelGroup.toLowerCase() !== devPersonnelGroup.toLowerCase()) continue
        }
      }
    }
    seen.add(key)
    result.push({
      value: key,
      label: personnelGroup ? `${optionValue} (${personnelGroup})` : optionValue,
      optionValue,
      personnelGroup,
      staffGroup,
    })
  }
  return result.sort((a, b) => a.label.localeCompare(b.label, 'zh-Hans-CN'))
})
// 编辑模式下若原专案已被删除，仍需在下拉框中保留当前值
const formProgramGroupOptionsWithCurrent = computed<FormProgramGroupItem[]>(() => {
  const list = formProgramGroupOptions.value
  if (!formProgramGroupKey.value) return list
  if (list.some(item => item.value === formProgramGroupKey.value)) return list
  const optionValue = form.programGroup
  const personnelGroup = form.personnelGroup || ''
  const staffGroup = formDeveloperRecord.value?.staffGroup || ''
  if (!optionValue) return list
  return [{
    value: formProgramGroupKey.value,
    label: personnelGroup ? `${optionValue} (${personnelGroup})` : optionValue,
    optionValue,
    personnelGroup,
    staffGroup,
  }, ...list]
})
// 开发人变化时，若当前已选专案不符合新开发人的人员组范围，则清空专案/程序名并重新筛选
watch(() => form.developer, () => {
  if (!formProgramGroupKey.value) return
  const validList = formProgramGroupOptionsWithCurrent.value
  if (validList.some(item => item.value === formProgramGroupKey.value)) return
  formProgramGroupKey.value = ''
  form.programGroup = ''
  form.personnelGroup = ''
  form.programName = ''
  options.programNames = []
})
const filteredPersonnelGroups = computed(() => {
  if (!isAdmin.value) return options.personnelGroups
  if (!filters.staffGroup) return options.personnelGroups
  const sg = filters.staffGroup
  return projectOptions.value
    .filter(item => item.optionType === 'PERSONNEL_GROUP' && (item.staffGroup || '') === sg)
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(item => item.optionValue)
})
const visibleOptionTypeDefinitions = computed(() =>
  hasPermission('OPTION_MANAGE')
    ? optionTypeDefinitions
    : optionTypeDefinitions.filter((item) =>
        item.type === 'PROGRAM_GROUP' || item.type === 'PROGRAM_NAME'))
const settingProgramGroups = computed(() =>
  Array.from(new Set(projectOptions.value
    .filter((item) => item.optionType === 'PROGRAM_GROUP')
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => item.optionValue))))
const settingStaffGroups = computed(() =>
  Array.from(new Set(projectOptions.value
    .filter((item) => item.optionType === 'STAFF_GROUP')
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => item.optionValue))))
const settingPersonnelGroups = computed(() =>
  Array.from(new Set(projectOptions.value
    .filter((item) => item.optionType === 'PERSONNEL_GROUP')
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => item.optionValue))))
const filteredSettingPersonnelGroups = computed(() => {
  if (!optionForm.staffGroup) return settingPersonnelGroups.value
  return Array.from(new Set(projectOptions.value
    .filter((item) =>
      item.optionType === 'PERSONNEL_GROUP' &&
      (item.staffGroup || '') === optionForm.staffGroup &&
      (item.optionValue || '') !== optionForm.staffGroup) // 排除与项目组同名的选项
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => item.optionValue)))
})
const filteredSettingProgramGroupsForOption = computed(() => {
  if (!optionForm.personnelGroup) return settingProgramGroups.value
  return Array.from(new Set(projectOptions.value
    .filter((item) =>
      item.optionType === 'PROGRAM_GROUP' &&
      (item.personnelGroup || '') === optionForm.personnelGroup)
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => item.optionValue)))
})
const filteredPersonnelGroupsForPersonnel = computed(() => {
  if (!personnelForm.staffGroup) return settingPersonnelGroups.value
  return Array.from(new Set(projectOptions.value
    .filter((item) =>
      item.optionType === 'PERSONNEL_GROUP' &&
      (item.staffGroup || '') === personnelForm.staffGroup)
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map((item) => item.optionValue)))
})
const canUpdateSelected = computed(() =>
  hasPermission('VERSION_UPDATE') && !showDeleted.value && selectedRecords.value.length === 1)
const pageTitle = computed(() => ({
  records: '项目版本记录',
  logs: '操作日志',
  settings: '项目内容设置',
  personnel: '人员管理',
  'no-access': '暂无功能权限',
}[activeView.value]))
const pageSubtitle = computed(() => ({
  records: '统一管理项目版本、编译信息与发布状态',
  logs: '所有新增、更新、删除操作均留痕，普通用户仅可查看',
  settings: '维护新增版本记录中使用的项目类型、项目组、专案和程序选项',
  personnel: '维护操作人员账号，人员清单同时作为开发人和编译人的下拉选项来源',
  'no-access': '请联系系统管理员分配所需的功能权限',
}[activeView.value]))
const drawerTitle = computed(() => editingId.value ? '编辑版本记录' : '新增版本记录')
const optionDialogTitle = computed(() => editingOptionId.value ? '编辑项目可选项' : '新增项目可选项')
const filteredProjectOptions = computed(() =>
  projectOptions.value
    .filter((item) => item.optionType === selectedOptionType.value)
    .filter((item) => {
      if (!optionStaffGroupFilter.value) return true
      return (item.staffGroup || '') === optionStaffGroupFilter.value
    })
    .filter((item) => {
      if (!optionPersonnelGroupFilter.value) return true
      return (item.personnelGroup || '') === optionPersonnelGroupFilter.value
    })
    .filter((item) => {
      if (!optionProgramGroupFilter.value) return true
      return (item.programGroup || '') === optionProgramGroupFilter.value
    })
    .filter((item) => {
      const kw = optionKeywordFilter.value.trim().toLowerCase()
      if (!kw) return true
      return (item.optionValue || '').toLowerCase().includes(kw)
        || (item.staffGroup || '').toLowerCase().includes(kw)
        || (item.personnelGroup || '').toLowerCase().includes(kw)
        || (item.programGroup || '').toLowerCase().includes(kw)
    }))
const duplicateOptionValues = computed(() => {
  const counts = new Map<string, number>()
  for (const item of filteredProjectOptions.value) {
    const key = (item.optionValue || '').trim().toLowerCase()
    if (key) counts.set(key, (counts.get(key) || 0) + 1)
  }
  return new Set([...counts.entries()].filter(([, c]) => c > 1).map(([k]) => k))
})
function optionRowClassName({ row }: { row: ProjectOption }) {
  const key = (row.optionValue || '').trim().toLowerCase()
  return duplicateOptionValues.value.has(key) ? 'option-row-duplicate' : ''
}
const currentOptionDefinition = computed(() =>
  optionTypeDefinitions.find((item) => item.type === selectedOptionType.value)!)
const canFilterByStaffGroup = computed(() =>
  selectedOptionType.value === 'PERSONNEL_GROUP' ||
  selectedOptionType.value === 'PROGRAM_GROUP' ||
  selectedOptionType.value === 'PROGRAM_NAME')
const canFilterByPersonnelGroup = computed(() =>
  selectedOptionType.value === 'PROGRAM_GROUP' ||
  selectedOptionType.value === 'PROGRAM_NAME')
const canFilterByProgramGroup = computed(() =>
  selectedOptionType.value === 'PROGRAM_NAME')
// 非管理员且项目组与人员组名称不同时，锁定人员组为用户所在人员组
const isOptionPersonnelGroupLocked = computed(() => {
  if (isAdmin.value) return false
  const sg = currentStaffGroup.value
  const pg = user.value?.personnelGroup || ''
  return Boolean(sg && pg && sg !== pg)
})

const todayText = () => {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}
const normalizeDate = (value?: string) => value ? value.replace('Z', '').slice(0, 19) : ''
const dateOnly = (value?: string) => value ? value.slice(0, 10) : ''
const formatDate = (value?: string) => {
  if (!value) return '—'
  return value.replace('T', ' ').replace('Z', '').slice(0, 19)
}
const statusLabel = (status: string) => ({
  RELEASED: '已释放',
  CANCELED: '已取消',
  PENDING: '待释放',
  WAIT_COMPILE: '待编译',
  COMPILED: '已编译',
  PRE_RELEASED: '提前释放',
  COMMENTED: '已注释',
}[status] || status)
const statusType = (status: string) => ({
  RELEASED: 'success',
  CANCELED: 'info',
  PENDING: 'warning',
  WAIT_COMPILE: 'warning',
  COMPILED: 'primary',
  PRE_RELEASED: 'primary',
  COMMENTED: 'info',
}[status] || 'info') as 'success' | 'warning' | 'primary' | 'info'

const queryParams = (): VersionQuery => ({
  page: currentPage.value,
  pageSize: pageSize.value,
  keyword: filters.keyword || undefined,
  projectType: filters.projectType || undefined,
  developer: filters.developer || undefined,
  compiler: filters.compiler || undefined,
  programGroup: filters.programGroup || undefined,
  programName: filters.programName || undefined,
  staffGroup: filters.staffGroup || undefined,
  personnelGroup: filters.personnelGroup || undefined,
  status: filters.status || undefined,
  startDate: filters.startDate || undefined,
  endDate: filters.endDate || undefined,
  deletedOnly: showDeleted.value || undefined,
  excludeReleased: !showDeleted.value && !filters.status,
})
const logQueryParams = (): LogQuery => ({
  page: logPage.value,
  pageSize: logPageSize.value,
  keyword: logFilters.keyword || undefined,
  operationType: logFilters.operationType || undefined,
  startTime: logFilters.startTime || undefined,
  endTime: logFilters.endTime || undefined,
})

async function loadDictionaries() {
  try {
    const [types, developers, staffGroups, personnelGroups, groups, groupOptions, programs, compilers] = await Promise.all([
      dictionariesApi.projectTypes(),
      dictionariesApi.developers(),
      dictionariesApi.staffGroups(),
      dictionariesApi.personnelGroups(),
      dictionariesApi.programGroups(),
      dictionariesApi.programGroupOptions(),
      dictionariesApi.programNames(),
      dictionariesApi.compilers(),
    ])
    options.projectTypes = types
    options.developers = developers
    options.staffGroups = staffGroups
    options.personnelGroups = personnelGroups
    options.programGroups = groups
    options.programGroupOptions = groupOptions
    options.programNames = programs
    options.compilers = compilers
  } catch (error) {
    ElMessage.warning(getErrorMessage(error, '项目可选项加载失败，请联系管理员检查设置'))
  }
}

async function loadProgramNames(programGroup: string, personnelGroup?: string) {
  if (!programGroup) {
    options.programNames = []
    return
  }
  try {
    options.programNames = await dictionariesApi.programNames(programGroup, personnelGroup)
  } catch (error) {
    options.programNames = []
    ElMessage.warning(getErrorMessage(error, '所属项目组的程序名加载失败'))
  }
}

async function onProgramGroupChange() {
  form.programName = ''
  const selected = formProgramGroupOptionsWithCurrent.value
    .find(item => item.value === formProgramGroupKey.value)
  form.programGroup = selected?.optionValue || ''
  form.personnelGroup = selected?.personnelGroup || ''
  formRef.value?.validateField('programGroup')
  await loadProgramNames(form.programGroup, form.personnelGroup)
}

function onOptionProgramGroupChange() {
  if (optionForm.optionType !== 'PROGRAM_NAME') return
  const matches = projectOptions.value.filter(
    (item) => item.optionType === 'PROGRAM_GROUP' && item.optionValue === optionForm.programGroup,
  )
  if (matches.length === 0) {
    optionForm.personnelGroup = ''
    optionForm.staffGroup = ''
    return
  }
  // 优先匹配当前已设置的人员组（锁定用户所在人员组，不因同名专案而跳转）
  const currentPg = optionForm.personnelGroup
  const matched = currentPg
    ? matches.find((m) => (m.personnelGroup || '') === currentPg) || matches[0]
    : matches[0]
  // 仅当匹配到的人员组与当前不同时才更新（但不强制覆盖为其他人员组）
  if (!currentPg || currentPg === (matched.personnelGroup || '')) {
    optionForm.personnelGroup = matched.personnelGroup || ''
  }
  optionForm.staffGroup = matched.staffGroup || ''
}

function onOptionStaffGroupChange() {
  optionForm.personnelGroup = ''
  if (optionForm.optionType === 'PROGRAM_NAME') {
    optionForm.programGroup = ''
  }
}

function onOptionPersonnelGroupChange() {
  if (optionForm.optionType === 'PROGRAM_NAME') {
    optionForm.programGroup = ''
  }
}

function onPersonnelStaffGroupChange() {
  personnelForm.personnelGroup = ''
}

async function fetchProjectOptions() {
  optionLoading.value = true
  try {
    projectOptions.value = await projectOptionsApi.list()
    if (!visibleOptionTypeDefinitions.value.some(
      (definition) => definition.type === selectedOptionType.value)) {
      const defaultType = visibleOptionTypeDefinitions.value[0]?.type || 'PROGRAM_GROUP'
      selectOptionType(defaultType)
    }
    selectedProjectOptions.value = []
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '项目可选项加载失败'))
  } finally {
    optionLoading.value = false
  }
}

async function refreshProjectSettings() {
  await fetchProjectOptions()
  if (hasPermission('VERSION_VIEW')) await loadDictionaries()
}

async function fetchRecords() {
  pageLoading.value = true
  try {
    const [result, counts] = await Promise.all([
      versionsApi.list(queryParams()),
      versionsApi.statusCounts(),
    ])
    records.value = result.items
    total.value = result.total
    Object.assign(statusCounts, counts)
    selectedRecords.value = []
  } catch (error) {
    ElMessage.error(getErrorMessage(error))
  } finally {
    pageLoading.value = false
  }
}

// 点击统计卡片按状态筛选查询
function filterByStatus(status: string) {
  // 再次点击相同状态视为取消筛选，回到默认（未释放）
  filters.status = filters.status === status ? 'UNRELEASED' : status
  currentPage.value = 1
  fetchRecords()
}

async function fetchLogs() {
  pageLoading.value = true
  try {
    const result = await logsApi.list(logQueryParams())
    logs.value = result.items
    logTotal.value = result.total
  } catch (error) {
    ElMessage.error(getErrorMessage(error))
  } finally {
    pageLoading.value = false
  }
}

async function fetchPersonnel() {
  personnelLoading.value = true
  try {
    personnel.value = await personnelApi.list()
  } catch (error) {
    const status = (error as { response?: { status?: number } })?.response?.status
    if (status !== 401 && status !== 403) {
      ElMessage.error(getErrorMessage(error, '人员列表加载失败'))
    }
  } finally {
    personnelLoading.value = false
  }
}

async function refreshPage() {
  if (!isLoggedIn.value) return
  if (activeView.value === 'no-access') return
  const canAccessPersonnel = hasPermission('PERSONNEL_MANAGE')
  if (activeView.value === 'records') {
    if (canAccessPersonnel) {
      await Promise.all([fetchRecords(), loadDictionaries(), fetchPersonnel()])
    } else {
      await Promise.all([fetchRecords(), loadDictionaries()])
    }
    // 顺便加载 projectOptions，使 filteredProgramGroups / 新增表单的专案筛选无需先访问项目设置
    if (hasPermission('OPTION_MANAGE') || hasPermission('PROGRAM_RELATION_MANAGE') || !isAdmin.value) {
      fetchProjectOptions().catch(() => { /* 回退使用 dictionaries */ })
    }
  } else if (activeView.value === 'logs') {
    await fetchLogs()
  } else if (activeView.value === 'personnel') {
    await fetchPersonnel()
  } else {
    await fetchProjectOptions()
  }
}

async function submitLogin() {
  if (!loginForm.username || !loginForm.password) {
    loginError.value = '请输入用户名和密码'
    return
  }
  loginLoading.value = true
  loginError.value = ''
  try {
    const result = await authApi.login(loginForm.username, loginForm.password)
    token.value = result.token
    user.value = result
    sessionStorage.setItem('pvm_token', result.token)
    sessionStorage.setItem('pvm_user', JSON.stringify(result))
    currentPage.value = 1
    Object.assign(filters, { keyword: '', projectType: '', developer: '', compiler: '', programGroup: '', programName: '', staffGroup: '', personnelGroup: '', status: 'UNRELEASED', startDate: '', endDate: '' })
    activeView.value = firstAccessibleView()
    await loadDictionaries()
    if (!isAdmin.value) {
      const name = (result.displayName || result.username || '').trim()
      if (name && options.developers.some((d) => d.toLocaleLowerCase() === name.toLocaleLowerCase())) {
        filters.developer = name
      }
    }
    await fetchRecords()
    // 登录后 records 视图立即需要可用的专案名称下拉：顺便加载 projectOptions
    if (activeView.value === 'records' && !isAdmin.value) {
      fetchProjectOptions().catch(() => { /* 回退使用 dictionaries */ })
    }
  } catch (error) {
    loginError.value = getErrorMessage(error, '用户名或密码错误')
  } finally {
    loginLoading.value = false
  }
}

async function logout() {
  try { await authApi.logout() } catch { /* token may already be expired */ }
  token.value = ''
  user.value = null
  sessionStorage.removeItem('pvm_token')
  sessionStorage.removeItem('pvm_user')
}

function openChangePassword() {
  Object.assign(changePasswordForm, {
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  })
  changePasswordDialogVisible.value = true
}

async function submitChangePassword() {
  if (!changePasswordForm.currentPassword) {
    ElMessage.warning('请输入当前密码')
    return
  }
  if (changePasswordForm.newPassword.length < 6) {
    ElMessage.warning('新密码至少需要 6 位')
    return
  }
  if (changePasswordForm.newPassword !== changePasswordForm.confirmPassword) {
    ElMessage.warning('两次输入的新密码不一致')
    return
  }
  changePasswordLoading.value = true
  try {
    await authApi.changePassword(
      changePasswordForm.currentPassword,
      changePasswordForm.newPassword,
    )
    ElMessage.success('密码修改成功，请使用新密码重新登录')
    changePasswordDialogVisible.value = false
    loginForm.password = ''
    await logout()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '密码修改失败'))
  } finally {
    changePasswordLoading.value = false
  }
}

function canAccessView(view: 'records' | 'logs' | 'settings' | 'personnel') {
  const permissionByView = {
    records: 'VERSION_VIEW',
    logs: 'LOG_VIEW',
    settings: 'OPTION_MANAGE',
    personnel: 'PERSONNEL_MANAGE',
  }
  return view === 'settings'
    ? hasPermission('OPTION_MANAGE') || hasPermission('PROGRAM_RELATION_MANAGE')
    : hasPermission(permissionByView[view])
}

function firstAccessibleView(): 'records' | 'logs' | 'settings' | 'personnel' | 'no-access' {
  return (['records', 'logs', 'settings', 'personnel'] as const)
    .find((view) => canAccessView(view)) || 'no-access'
}

function switchView(view: 'records' | 'logs' | 'settings' | 'personnel') {
  if (!canAccessView(view)) {
    ElMessage.warning('当前账号没有此功能权限')
    return
  }
  activeView.value = view
  if (view === 'records') fetchRecords()
  else if (view === 'logs') fetchLogs()
  else if (view === 'personnel') fetchPersonnel()
  else fetchProjectOptions()
}

function openPersonnelCreate() {
  personnelDialogMode.value = 'create'
  const autoStaffGroup = !isAdmin.value ? currentStaffGroup.value : ''
  const autoPersonnelGroup = !isAdmin.value ? (user.value?.personnelGroup || '') : ''
  Object.assign(personnelForm, {
    id: '',
    employeeNumber: '',
    personnelName: '',
    managementPassword: '',
    staffGroup: autoStaffGroup,
    personnelGroup: autoPersonnelGroup,
  })
  personnelDialogVisible.value = true
}

function openPersonnelEdit(item: Personnel) {
  personnelDialogMode.value = 'edit'
  const autoStaffGroup = !isAdmin.value ? currentStaffGroup.value : ''
  const autoPersonnelGroup = !isAdmin.value ? (user.value?.personnelGroup || '') : ''
  Object.assign(personnelForm, {
    id: item.id,
    employeeNumber: item.employeeNumber,
    personnelName: item.personnelName,
    managementPassword: '',
    staffGroup: autoStaffGroup || item.staffGroup || '',
    personnelGroup: autoPersonnelGroup || item.personnelGroup || '',
  })
  personnelDialogVisible.value = true
}

async function savePersonnel() {
  try {
    await personnelFormRef.value?.validate()
  } catch {
    return
  }
  personnelSaving.value = true
  try {
    if (personnelDialogMode.value === 'create') {
      await personnelApi.create({
        employeeNumber: personnelForm.employeeNumber.trim(),
        personnelName: personnelForm.personnelName.trim(),
        managementPassword: personnelForm.managementPassword,
        staffGroup: personnelForm.staffGroup?.trim() || undefined,
        personnelGroup: personnelForm.personnelGroup?.trim() || undefined,
      })
      ElMessage.success('操作人员创建成功，已加入开发人和编译人下拉选项')
    } else {
      await personnelApi.update(personnelForm.id, {
        employeeNumber: personnelForm.employeeNumber.trim(),
        personnelName: personnelForm.personnelName.trim(),
        staffGroup: personnelForm.staffGroup?.trim() || undefined,
        personnelGroup: personnelForm.personnelGroup?.trim() || undefined,
      })
      ElMessage.success('人员信息更新成功')
    }
    personnelDialogVisible.value = false
    await Promise.all([fetchPersonnel(), loadDictionaries()])
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '人员信息保存失败'))
  } finally {
    personnelSaving.value = false
  }
}

function openResetPersonnelPassword(item: Personnel) {
  resetPasswordId.value = item.id
  resetPasswordName.value = item.personnelName
  resetPassword.value = ''
  resetPasswordDialogVisible.value = true
}

async function submitResetPersonnelPassword() {
  if (resetPassword.value.length < 6) {
    ElMessage.warning('管理密码至少 6 位')
    return
  }
  resetPasswordLoading.value = true
  try {
    await personnelApi.resetPassword(resetPasswordId.value, resetPassword.value)
    resetPasswordDialogVisible.value = false
    ElMessage.success('管理密码重置成功')
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '管理密码重置失败'))
  } finally {
    resetPasswordLoading.value = false
  }
}

function openPersonnelPermissions(item: Personnel) {
  permissionPersonnelId.value = item.id
  permissionPersonnelName.value = item.personnelName
  selectedPermissions.value = [...item.permissions]
  permissionDialogVisible.value = true
}

async function savePersonnelPermissions() {
  permissionSaving.value = true
  try {
    await personnelApi.updatePermissions(
      permissionPersonnelId.value,
      selectedPermissions.value,
    )
    permissionDialogVisible.value = false
    ElMessage.success('人员权限更新成功，用户重新登录后生效')
    await fetchPersonnel()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '人员权限更新失败'))
  } finally {
    permissionSaving.value = false
  }
}

async function deletePersonnel(item: Personnel) {
  try {
    await ElMessageBox.confirm(
      `确定删除操作人员“${item.employeeNumber} / ${item.personnelName}”吗？删除后该账号将无法登录，且不再出现在开发人和编译人下拉选项中。`,
      '删除操作人员',
      { type: 'warning', confirmButtonText: '确定删除', cancelButtonText: '取消' },
    )
    await personnelApi.remove(item.id)
    ElMessage.success('操作人员已删除')
    await Promise.all([fetchPersonnel(), loadDictionaries()])
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '删除操作人员失败'))
    }
  }
}

function optionCount(optionType: ProjectOptionType) {
  return projectOptions.value.filter((item) => item.optionType === optionType).length
}

function selectOptionType(optionType: ProjectOptionType) {
  selectedOptionType.value = optionType
  const needStaffGroup = optionType === 'PERSONNEL_GROUP' || optionType === 'PROGRAM_GROUP' || optionType === 'PROGRAM_NAME'
  const needPersonnelGroup = optionType === 'PROGRAM_GROUP' || optionType === 'PROGRAM_NAME'
  // 非管理员用户默认按自己所在项目组筛选；管理员默认不筛选
  if (!isAdmin.value && needStaffGroup) {
    optionStaffGroupFilter.value = currentStaffGroup.value
  } else {
    optionStaffGroupFilter.value = ''
  }
  if (!isAdmin.value && needPersonnelGroup) {
    const sg = currentStaffGroup.value
    const pg = user.value?.personnelGroup || ''
    // 项目组与人员组名称相同时，不按人员组筛选，显示项目组全部信息
    optionPersonnelGroupFilter.value = (sg && pg && sg === pg) ? '' : pg
  } else {
    optionPersonnelGroupFilter.value = ''
  }
  optionProgramGroupFilter.value = ''
  optionKeywordFilter.value = ''
  selectedProjectOptions.value = []
}

function withCurrentOption(items: string[], current?: string) {
  if (!current || items.some((item) => item.toLocaleLowerCase() === current.toLocaleLowerCase())) {
    return items
  }
  return [current, ...items]
}

function openOptionCreate() {
  editingOptionId.value = null
  const maxSort = filteredProjectOptions.value.reduce(
    (maximum, item) => Math.max(maximum, item.sortOrder),
    0,
  )
  const autoStaffGroup = !isAdmin.value ? currentStaffGroup.value : ''
  const userPg = user.value?.personnelGroup || ''
  // 项目组与人员组名称不同时，自动锁定用户所在人员组；相同时默认空，用户下拉选择
  const autoPersonnelGroup = (!isAdmin.value && autoStaffGroup && userPg && autoStaffGroup !== userPg) ? userPg : ''
  Object.assign(optionForm, {
    optionType: selectedOptionType.value,
    optionValue: '',
    staffGroup: autoStaffGroup,
    personnelGroup: autoPersonnelGroup,
    programGroup: '',
    sortOrder: maxSort + 10,
    rowVersion: 0,
  })
  optionDialogVisible.value = true
}

function openOptionEdit(item: ProjectOption) {
  editingOptionId.value = item.id
  const autoStaffGroup = !isAdmin.value ? currentStaffGroup.value : ''
  const autoPersonnelGroup = !isAdmin.value ? (user.value?.personnelGroup || '') : ''
  Object.assign(optionForm, {
    optionType: item.optionType,
    optionValue: item.optionValue,
    staffGroup: item.staffGroup || autoStaffGroup,
    personnelGroup: item.personnelGroup || autoPersonnelGroup,
    programGroup: item.programGroup || '',
    sortOrder: item.sortOrder,
    rowVersion: item.rowVersion,
  })
  optionDialogVisible.value = true
}

function editSelectedProjectOption() {
  if (selectedProjectOptions.value.length !== 1) {
    ElMessage.warning('修改操作必须且只能选择一个选项')
    return
  }
  openOptionEdit(selectedProjectOptions.value[0]!)
}

function deleteSelectedProjectOption() {
  if (selectedProjectOptions.value.length !== 1) {
    ElMessage.warning('删除操作必须且只能选择一个选项')
    return
  }
  deleteProjectOption(selectedProjectOptions.value[0]!)
}

function handleProjectOptionSelection(selection: ProjectOption[]) {
  selectedProjectOptions.value = selection
}

function openOptionImport() {
  optionImportFile.value = null
  optionImportInputKey.value++
  optionImportDialogVisible.value = true
}

function handleOptionImportFile(event: Event) {
  const input = event.target as HTMLInputElement
  optionImportFile.value = input.files?.[0] || null
}

async function importProjectOptions() {
  if (!optionImportFile.value) {
    ElMessage.warning('请先选择需要导入的 .xlsx 文件')
    return
  }
  optionImportLoading.value = true
  try {
    const result = await projectOptionsApi.import(optionImportFile.value)
    ElMessage.success(
      `成功导入 ${result.createdCount} 个选项，跳过 ${result.skippedDuplicateCount} 个重复项`,
    )
    optionImportDialogVisible.value = false
    await refreshProjectSettings()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '批量导入失败，请检查 Excel 内容'))
  } finally {
    optionImportLoading.value = false
  }
}

async function downloadOptionImportTemplate(optionType?: string) {
  optionTemplateLoading.value = true
  try {
    const response = await projectOptionsApi.downloadImportTemplate(optionType)
    const url = URL.createObjectURL(response.data)
    const link = document.createElement('a')
    link.href = url
    link.download = optionType === 'PROGRAM_GROUP' ? '专案名称批量导入模板.xlsx' : '项目选项批量导入模板.xlsx'
    link.click()
    URL.revokeObjectURL(url)
    ElMessage.success('导入模板已下载')
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '导入模板下载失败'))
  } finally {
    optionTemplateLoading.value = false
  }
}

function openVersionImport() {
  versionImportFile.value = null
  versionImportInputKey.value++
  versionImportDialogVisible.value = true
}

function handleVersionImportFile(event: Event) {
  const input = event.target as HTMLInputElement
  versionImportFile.value = input.files?.[0] || null
}

async function downloadVersionImportTemplate() {
  versionTemplateLoading.value = true
  try {
    const response = await versionsApi.downloadImportTemplate()
    const url = URL.createObjectURL(response.data)
    const link = document.createElement('a')
    link.href = url
    link.download = '项目版本批量导入模板.xlsx'
    link.click()
    URL.revokeObjectURL(url)
    ElMessage.success('导入模板已下载')
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '导入模板下载失败'))
  } finally {
    versionTemplateLoading.value = false
  }
}

async function importProjectVersions() {
  if (!versionImportFile.value) {
    ElMessage.warning('请先选择需要导入的 .xlsx 文件')
    return
  }
  versionImportLoading.value = true
  try {
    const result = await versionsApi.import(versionImportFile.value)
    ElMessage.success(
      `成功导入 ${result.createdCount} 条版本记录，跳过 ${result.skippedDuplicateCount} 条重复记录`,
    )
    versionImportDialogVisible.value = false
    currentPage.value = 1
    await fetchRecords()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '批量导入失败，请检查 Excel 内容'))
  } finally {
    versionImportLoading.value = false
  }
}

async function saveProjectOption() {
  try {
    await optionFormRef.value?.validate()
    const autoStaffGroup = !isAdmin.value ? currentStaffGroup.value : ''
    const payload = {
      ...optionForm,
      optionValue: optionForm.optionValue.trim(),
      staffGroup: (() => {
        if (optionForm.optionType === 'PERSONNEL_GROUP') {
          return isAdmin.value ? (optionForm.staffGroup?.trim() || undefined) : autoStaffGroup
        }
        if (optionForm.optionType === 'PROGRAM_GROUP') {
          return isAdmin.value ? (optionForm.staffGroup?.trim() || undefined) : autoStaffGroup
        }
        if (optionForm.optionType === 'PROGRAM_NAME' && optionForm.staffGroup?.trim()) {
          return optionForm.staffGroup.trim()
        }
        return undefined
      })(),
      personnelGroup: (optionForm.optionType === 'PROGRAM_GROUP' || optionForm.optionType === 'PROGRAM_NAME')
        ? (optionForm.personnelGroup?.trim() || undefined)
        : undefined,
      programGroup: optionForm.optionType === 'PROGRAM_NAME'
        ? optionForm.programGroup?.trim()
        : undefined,
    }
    if (editingOptionId.value) {
      await projectOptionsApi.update(editingOptionId.value, payload)
      ElMessage.success('项目可选项更新成功')
    } else {
      await projectOptionsApi.create(payload)
      ElMessage.success('项目可选项创建成功')
    }
    optionDialogVisible.value = false
    selectedOptionType.value = payload.optionType
    await refreshProjectSettings()
  } catch (error) {
    if (error !== false) ElMessage.error(getErrorMessage(error, '项目可选项保存失败'))
  }
}

async function deleteProjectOption(item: ProjectOption) {
  try {
    await ElMessageBox.confirm(
      `确定删除“${item.optionValue}”吗？已有版本记录会保留原值，但新记录将不能再选择该内容。`,
      '删除项目可选项',
      { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' },
    )
    await projectOptionsApi.remove(item.id, item.rowVersion)
    ElMessage.success('项目可选项删除成功')
    await refreshProjectSettings()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '项目可选项删除失败'))
    }
  }
}

function resetFilters() {
  Object.assign(filters, { keyword: '', projectType: '', developer: '', compiler: '', programGroup: '', programName: '', staffGroup: '', personnelGroup: '', status: 'UNRELEASED', startDate: '', endDate: '' })
  currentPage.value = 1
  fetchRecords()
}

function resetLogFilters() {
  Object.assign(logFilters, { keyword: '', operationType: '', startTime: '', endTime: '' })
  logPage.value = 1
  fetchLogs()
}

function clearForm() {
  Object.assign(form, {
    projectDate: todayText(),
    projectType: '',
    developer: '',
    programGroup: '',
    personnelGroup: '',
    programName: '',
    programVersion: '',
    compiler: '',
    compileTime: '',
    compiledProgramVersion: '',
    versionDescription: '',
    releaseTime: '',
    branchName: '',
    remark1: '',
    remark2: '',
    status: 'WAIT_COMPILE',
    rowVersion: 0,
  })
  formProgramGroupKey.value = ''
}

async function openCreate() {
  const needPersonnel = hasPermission('PERSONNEL_MANAGE')
  if (!filteredDevelopers.value.length || !options.programGroupOptions.length ||
      (needPersonnel && !personnel.value.length)) {
    const tasks: Promise<unknown>[] = [loadDictionaries()]
    if (needPersonnel) tasks.push(fetchPersonnel())
    await Promise.all(tasks)
  }
  editingId.value = null
  clearForm()
  options.programNames = []
  const currentPersonnelName = displayName.value.trim()
  if (currentPersonnelName &&
      filteredDevelopers.value.some((item) =>
        item.toLocaleLowerCase() === currentPersonnelName.toLocaleLowerCase())) {
    form.developer = currentPersonnelName
  }
  drawerVisible.value = true
}

function openEdit(record: ProjectVersion) {
  editingId.value = record.id
  Object.assign(form, {
    ...record,
    projectDate: dateOnly(record.projectDate),
    compileTime: normalizeDate(record.compileTime),
    releaseTime: normalizeDate(record.releaseTime),
  })
  formProgramGroupKey.value = `${record.programGroup}\u0001${record.personnelGroup || ''}`
  void loadProgramNames(record.programGroup, record.personnelGroup)
  drawerVisible.value = true
}

function openDetail(record: ProjectVersion) {
  detailRecord.value = record
  detailVisible.value = true
}

function updateDetailRecord() {
  if (!detailRecord.value) return
  const record = detailRecord.value
  detailVisible.value = false
  openEdit(record)
}

async function deleteDetailRecord() {
  if (!detailRecord.value) return
  const record = detailRecord.value
  try {
    await ElMessageBox.confirm(
      `确定将“${record.programGroup} / ${record.programName} ${record.programVersion}”标记为已删除吗？删除后可在回收站恢复。`,
      '删除版本记录',
      { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' },
    )
    await versionsApi.remove(record.id, record.rowVersion)
    ElMessage.success('版本记录已标记为删除')
    detailVisible.value = false
    detailRecord.value = null
    await fetchRecords()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '删除版本记录失败'))
    }
  }
}

async function permanentlyDeleteDetailRecord() {
  if (!detailRecord.value || !isAdmin.value) return
  const record = detailRecord.value
  try {
    await ElMessageBox.confirm(
      `确定物理删除“${record.programGroup} / ${record.programName} ${record.programVersion}”吗？该操作不可恢复。`,
      '管理员物理删除',
      { type: 'error', confirmButtonText: '确认物理删除', cancelButtonText: '取消' },
    )
    await versionsApi.permanentRemove(record.id, record.rowVersion)
    ElMessage.success('版本记录已物理删除')
    detailVisible.value = false
    detailRecord.value = null
    await fetchRecords()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '物理删除失败'))
    }
  }
}

function handleSelectionChange(selection: ProjectVersion[]) {
  selectedRecords.value = selection
}

function updateSelectedRecord() {
  if (selectedRecords.value.length !== 1) {
    ElMessage.warning('更新操作必须且只能勾选一条记录')
    return
  }
  openEdit(selectedRecords.value[0]!)
}

async function saveRecord() {
  if (recordSaving.value) return
  try {
    await formRef.value?.validate()
  } catch {
    ElMessage.warning('请先完善表单中标红的必填项')
    recordSaving.value = false
    return
  }
  recordSaving.value = true
  try {
    const payload: ProjectVersionForm = {
      ...form,
      compiler: form.compiler || undefined,
      compileTime: form.compileTime || undefined,
      releaseTime: form.releaseTime || undefined,
    }
    if (editingId.value) {
      await versionsApi.update(editingId.value, payload)
      ElMessage.success('版本记录更新成功')
    } else {
      await versionsApi.create(payload)
      ElMessage.success('版本记录创建成功')
    }
    drawerVisible.value = false
    await Promise.all([fetchRecords(), loadDictionaries()])
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '保存失败，请检查输入'))
  } finally {
    recordSaving.value = false
  }
}

async function deleteSelectedRecords() {
  if (!selectedRecords.value.length) {
    ElMessage.warning('请至少勾选一条需要删除的记录')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定将已勾选的 ${selectedRecords.value.length} 条记录移入回收站吗？删除后不会物理清除，可在回收站恢复。`,
      '批量删除确认',
      { type: 'warning', confirmButtonText: `确认删除 ${selectedRecords.value.length} 条`, cancelButtonText: '取消' },
    )
    const count = selectedRecords.value.length
    await versionsApi.batchDelete(selectedRecords.value.map(({ id, rowVersion }) => ({ id, rowVersion })))
    ElMessage.success(`已将 ${count} 条记录移入回收站`)
    await fetchRecords()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error(getErrorMessage(error, '删除失败'))
  }
}

async function restoreSelectedRecords() {
  if (!selectedRecords.value.length) {
    ElMessage.warning('请至少勾选一条需要恢复的记录')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确定恢复已勾选的 ${selectedRecords.value.length} 条记录吗？`,
      '批量恢复确认',
      { type: 'info', confirmButtonText: `恢复 ${selectedRecords.value.length} 条`, cancelButtonText: '取消' },
    )
    const count = selectedRecords.value.length
    await versionsApi.batchRestore(selectedRecords.value.map(({ id, rowVersion }) => ({ id, rowVersion })))
    ElMessage.success(`已恢复 ${count} 条记录`)
    await fetchRecords()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error(getErrorMessage(error, '恢复失败'))
  }
}

async function toggleRecycleBin() {
  showDeleted.value = !showDeleted.value
  currentPage.value = 1
  await fetchRecords()
}

async function exportRecords() {
  if (total.value <= 0) {
    ElMessage.warning('当前筛选条件下没有可导出的记录')
    return
  }
  if (exportLoading.value) return
  exportLoading.value = true
  try {
    const response = await versionsApi.export(queryParams())
    const url = URL.createObjectURL(response.data)
    const link = document.createElement('a')
    link.href = url
    link.download = `项目版本_${todayText()}.xlsx`
    link.click()
    URL.revokeObjectURL(url)
    ElMessage.success(`已导出 ${total.value} 条记录`)
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '导出失败'))
  } finally {
    exportLoading.value = false
  }
}

function onPageChange(page: number) {
  currentPage.value = page
  fetchRecords()
}
function onLogPageChange(page: number) {
  logPage.value = page
  fetchLogs()
}

window.addEventListener('pvm:unauthorized', () => {
  token.value = ''
  user.value = null
})

onMounted(() => {
  if (isLoggedIn.value) {
    if (activeView.value === 'no-access' || !canAccessView(activeView.value)) {
      activeView.value = firstAccessibleView()
    }
    refreshPage()
  }
})
</script>

<template>
  <div v-if="!isLoggedIn" class="login-page">
    <div class="login-orbit orbit-one"></div>
    <div class="login-orbit orbit-two"></div>
    <div class="login-shell">
      <section class="login-brand-panel">
        <div class="brand-kicker"><span class="brand-dot"></span> MES · ENGINEERING PLATFORM</div>
        <div class="brand-symbol">PV<span>M</span></div>
        <h1>项目版本<br /><em>管理系统</em></h1>
        <p>让每一次发布都可追溯，让每一个版本都清晰可见。</p>
        <div class="brand-lines"><i></i><i></i><i></i></div>
        <div class="brand-footnote">INTERNAL RELEASE CONTROL · v1.0</div>
      </section>
      <section class="login-card">
        <div class="mobile-brand">PVM <span>项目版本管理</span></div>
        <div class="login-heading">
          <span class="eyebrow">WELCOME BACK</span>
          <h2>欢迎回来</h2>
          <p>登录以进入项目版本管理台</p>
        </div>
        <el-form @submit.prevent="submitLogin">
          <el-form-item label="用户名">
            <el-input v-model="loginForm.username" size="large" placeholder="请输入用户名" :prefix-icon="User" autocomplete="username" @keyup.enter="focusPassword" />
          </el-form-item>
          <el-form-item label="密码">
            <el-input ref="passwordInputRef" v-model="loginForm.password" size="large" type="password" show-password placeholder="请输入密码" :prefix-icon="Lock" autocomplete="new-password" @keyup.enter="submitLogin" />
          </el-form-item>
          <div v-if="loginError" class="login-error"><Warning /> {{ loginError }}</div>
          <el-button class="login-submit" type="primary" size="large" :loading="loginLoading" @click="submitLogin">登录系统 <span>→</span></el-button>
        </el-form>
        <div class="login-security"><Lock /> 访问受控 · 数据操作全程留痕</div>
      </section>
    </div>
  </div>

  <div v-else class="app-shell" :class="{ 'sidebar-collapsed': sidebarCollapsed }">
    <aside class="sidebar">
      <div class="sidebar-head">
        <div class="sidebar-logo">
          <div class="sidebar-symbol">PV<span>M</span></div>
          <div class="sidebar-logo-text"><strong>版本管理台</strong><small>PROJECT VERSION</small></div>
        </div>
      </div>
      <div class="sidebar-section-label">工作台</div>
      <button v-if="hasPermission('VERSION_VIEW')" class="nav-item" :class="{ active: activeView === 'records' }" aria-label="项目版本" title="项目版本" @click="switchView('records')">
        <Collection /><span class="nav-text">项目版本</span><b v-if="activeView === 'records'"></b>
      </button>
      <button v-if="hasPermission('LOG_VIEW')" class="nav-item" :class="{ active: activeView === 'logs' }" aria-label="操作日志" title="操作日志" @click="switchView('logs')">
        <Tickets /><span class="nav-text">操作日志</span><b v-if="activeView === 'logs'"></b>
      </button>
      <button v-if="hasPermission('OPTION_MANAGE') || hasPermission('PROGRAM_RELATION_MANAGE')" class="nav-item" :class="{ active: activeView === 'settings' }" aria-label="项目设置" title="项目设置" @click="switchView('settings')">
        <Setting /><span class="nav-text">项目设置</span><b v-if="activeView === 'settings'"></b>
      </button>
      <button v-if="hasPermission('PERSONNEL_MANAGE')" class="nav-item" :class="{ active: activeView === 'personnel' }" aria-label="人员管理" title="人员管理" @click="switchView('personnel')">
        <User /><span class="nav-text">人员管理</span><b v-if="activeView === 'personnel'"></b>
      </button>
      <div class="sidebar-divider"></div>
      <div class="sidebar-hint"><div class="hint-icon"><CircleCheck /></div><div class="sidebar-hint-text"><strong>数据安全</strong><p>当前连接已启用<br />操作审计与乐观锁</p></div></div>
      <div class="sidebar-footer">PVM SYSTEM<br /><span>Internal use only</span></div>
    </aside>

    <button
      class="sidebar-floating-toggle"
      :class="{ collapsed: sidebarCollapsed }"
      :title="sidebarCollapsed ? '展开菜单' : '折叠菜单'"
      :style="{ left: sidebarCollapsed ? '62px' : '220px' }"
      @click="toggleSidebar"
      aria-label="切换侧边栏"
    >
      <span class="sidebar-toggle-icon">{{ sidebarCollapsed ? '»' : '«' }}</span>
    </button>

    <main class="main-content">
      <header class="topbar">
        <div class="topbar-page">
          <div class="breadcrumb">工作台 <span>/</span> <b>{{ pageTitle }}</b></div>
          <div v-if="activeView === 'records'" class="topbar-title-line">
            <h2>{{ pageTitle }}</h2>
            <p>{{ pageSubtitle }}</p>
          </div>
          <template v-else>
            <h2>{{ pageTitle }}</h2>
            <p>{{ pageSubtitle }}</p>
          </template>
        </div>
        <div class="topbar-actions">
          <el-button circle :icon="Refresh" :loading="pageLoading || optionLoading" title="刷新" @click="refreshPage" />
          <div class="topbar-divider"></div>
          <el-dropdown trigger="click">
            <div class="profile-trigger">
              <div class="avatar">{{ displayName.slice(0, 1).toUpperCase() }}</div>
              <div class="profile-meta"><strong>{{ displayName }}</strong><span>{{ isAdmin ? '系统管理员' : '普通用户' }}</span></div>
              <ArrowDown class="arrow-down" />
            </div>
            <template #dropdown>
              <el-dropdown-menu><el-dropdown-item :icon="Lock" @click="openChangePassword">修改密码</el-dropdown-item><el-dropdown-item divided @click="logout">退出登录</el-dropdown-item></el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </header>

      <section v-if="activeView === 'records'" class="page-body">
        <div class="stats-grid">
          <button type="button" class="stat-card clickable" :class="{ active: filters.status === 'UNRELEASED' }" @click="filterByStatus('UNRELEASED')">
            <div class="stat-label"><span class="stat-icon blue"><Document /></span><span>当前记录</span></div>
            <strong>{{ statusCounts.total }}</strong><small>未释放版本总量</small>
          </button>
          <button type="button" class="stat-card clickable" :class="{ active: filters.status === 'PENDING' }" @click="filterByStatus('PENDING')">
            <div class="stat-label"><span class="stat-icon amber"><Clock /></span><span>待释放</span></div>
            <strong>{{ statusCounts.pending }}</strong><small>等待正常释放</small>
          </button>
          <button type="button" class="stat-card clickable" :class="{ active: filters.status === 'WAIT_COMPILE' }" @click="filterByStatus('WAIT_COMPILE')">
            <div class="stat-label"><span class="stat-icon amber"><Clock /></span><span>待编译</span></div>
            <strong>{{ statusCounts.waitCompile }}</strong><small>等待程序编译</small>
          </button>
          <button type="button" class="stat-card clickable" :class="{ active: filters.status === 'COMPILED' }" @click="filterByStatus('COMPILED')">
            <div class="stat-label"><span class="stat-icon blue"><Collection /></span><span>已编译</span></div>
            <strong>{{ statusCounts.compiled }}</strong><small>已完成程序编译</small>
          </button>
          <button type="button" class="stat-card clickable" :class="{ active: filters.status === 'PRE_RELEASED' }" @click="filterByStatus('PRE_RELEASED')">
            <div class="stat-label"><span class="stat-icon purple"><Warning /></span><span>提前释放</span></div>
            <strong>{{ statusCounts.preReleased }}</strong><small>提前释放版本</small>
          </button>
          <button type="button" class="stat-card clickable" :class="{ active: filters.status === 'RELEASED' }" @click="filterByStatus('RELEASED')">
            <div class="stat-label"><span class="stat-icon green"><CircleCheck /></span><span>已释放</span></div>
            <strong>{{ statusCounts.released }}</strong><small>已完成释放</small>
          </button>
        </div>

        <div class="panel filter-panel">
          <div class="filter-head"><div class="filter-heading-copy"><h3>{{ showDeleted ? '筛选已删除记录' : '筛选记录' }}</h3><p>支持关键字与多条件组合查询</p></div><el-button text @click="resetFilters">重置条件</el-button></div>
          <div class="filter-grid">
            <el-input v-model="filters.keyword" clearable placeholder="搜索程序名、版本、分支…" :prefix-icon="Search" @keyup.enter="currentPage = 1; fetchRecords()" />
            <el-select v-model="filters.projectType" clearable filterable placeholder="项目类型"><el-option v-for="item in options.projectTypes" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-model="filters.developer" clearable filterable placeholder="开发人"><el-option v-for="item in filteredDevelopers" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-model="filters.compiler" clearable filterable placeholder="编译人"><el-option v-for="item in filteredCompilers" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-model="filters.programGroup" clearable filterable placeholder="专案名称" @change="filters.programName = ''"><el-option v-for="item in filteredProgramGroups" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-model="filters.programName" clearable filterable placeholder="程序名"><el-option v-for="item in filteredProgramNames" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-if="isAdmin" v-model="filters.staffGroup" clearable filterable placeholder="项目组" @change="filters.personnelGroup = ''"><el-option v-for="item in options.staffGroups" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-if="isAdmin" v-model="filters.personnelGroup" clearable filterable placeholder="人员组"><el-option v-for="item in filteredPersonnelGroups" :key="item" :label="item" :value="item" /></el-select>
            <el-select v-model="filters.status" clearable placeholder="状态">
              <el-option label="未释放" value="UNRELEASED" />
              <el-option label="待释放" value="PENDING" />
              <el-option label="待编译" value="WAIT_COMPILE" />
              <el-option label="已编译" value="COMPILED" />
              <el-option label="提前释放" value="PRE_RELEASED" />
              <el-option label="已释放" value="RELEASED" />
              <el-option label="已取消" value="CANCELED" />
              <el-option label="已注释" value="COMMENTED" />
            </el-select>
            <el-date-picker v-model="filters.startDate" type="date" value-format="YYYY-MM-DD" placeholder="开始日期" />
            <el-date-picker v-model="filters.endDate" type="date" value-format="YYYY-MM-DD" placeholder="结束日期" />
            <el-button type="primary" :icon="Search" @click="currentPage = 1; fetchRecords()">查询</el-button>
          </div>
        </div>

        <div v-if="hasPermission('VERSION_CREATE') || hasPermission('VERSION_UPDATE') || hasPermission('VERSION_DELETE') || hasPermission('VERSION_IMPORT')" class="panel action-panel">
          <div class="action-copy">
            <span class="action-icon"><Setting /></span>
            <div>
              <h3>{{ showDeleted ? '回收站操作' : '记录操作' }}</h3>
              <p v-if="selectedRecords.length">已勾选 <b>{{ selectedRecords.length }}</b> 条记录</p>
              <p v-else>{{ showDeleted ? '勾选记录后可批量恢复' : '更新只能选择一条，删除支持多选' }}</p>
            </div>
          </div>
          <div v-if="!showDeleted" class="record-actions">
            <el-button v-if="hasPermission('VERSION_CREATE')" type="primary" :icon="Plus" @click="openCreate">新增记录</el-button>
            <el-button v-if="hasPermission('VERSION_IMPORT')" :icon="Upload" @click="openVersionImport">批量导入</el-button>
            <el-button v-if="hasPermission('VERSION_UPDATE')" :icon="EditPen" :disabled="!canUpdateSelected" title="更新时必须且只能勾选一条记录" @click="updateSelectedRecord">更新记录</el-button>
            <el-button v-if="hasPermission('VERSION_DELETE')" type="danger" plain :icon="Delete" :disabled="selectedRecords.length === 0" @click="deleteSelectedRecords">删除所选<span v-if="selectedRecords.length">（{{ selectedRecords.length }}）</span></el-button>
            <el-button v-if="hasPermission('VERSION_DELETE')" :icon="RefreshLeft" @click="toggleRecycleBin">回收站</el-button>
          </div>
          <div v-else class="record-actions">
            <el-button type="success" :icon="RefreshLeft" :disabled="selectedRecords.length === 0" @click="restoreSelectedRecords">恢复所选<span v-if="selectedRecords.length">（{{ selectedRecords.length }}）</span></el-button>
            <el-button @click="toggleRecycleBin">返回有效记录</el-button>
          </div>
        </div>

        <div class="panel table-panel">
          <div class="panel-toolbar"><div><h3>{{ showDeleted ? '回收站' : '版本清单' }} <span>{{ total }}</span></h3><p>当前显示第 {{ total ? (currentPage - 1) * pageSize + 1 : 0 }}–{{ Math.min(currentPage * pageSize, total) }} 条 / 共 {{ total }} 条</p></div><div class="toolbar-actions"><el-button type="primary" plain :icon="Download" :loading="exportLoading" :disabled="exportLoading || total === 0" @click="exportRecords">导出清单<span v-if="total > 0" class="export-count">（{{ total }}）</span></el-button></div></div>
          <el-table v-loading="pageLoading" :data="records" class="version-table" height="520" stripe border row-key="id" @selection-change="handleSelectionChange">
            <el-table-column v-if="hasPermission('VERSION_UPDATE') || hasPermission('VERSION_DELETE')" type="selection" width="48" fixed="left" align="center" />
            <el-table-column type="index" label="#" width="54" fixed="left" align="center" />
            <el-table-column label="查看" width="78" fixed="left" align="center">
              <template #default="{ row }"><el-button link type="primary" :icon="View" @click="openDetail(row)">详情</el-button></template>
            </el-table-column>
            <el-table-column prop="status" label="状态" width="94" fixed="left" align="center"><template #default="{ row }"><el-tag :type="statusType(row.status)" effect="light">{{ statusLabel(row.status) }}</el-tag></template></el-table-column>
            <el-table-column prop="projectDate" label="项目时间" width="118"><template #default="{ row }">{{ dateOnly(row.projectDate) }}</template></el-table-column>
            <el-table-column prop="projectType" label="项目类型" width="110"><template #default="{ row }"><span class="code-pill">{{ row.projectType }}</span></template></el-table-column>
            <el-table-column prop="developer" label="开发人" width="108" show-overflow-tooltip />
            <el-table-column prop="programGroup" label="专案名称" width="140" show-overflow-tooltip />
            <el-table-column prop="personnelGroup" label="人员组" width="120" show-overflow-tooltip>
              <template #default="{ row }"><span v-if="row.personnelGroup">{{ row.personnelGroup }}</span><span v-else class="muted">—</span></template>
            </el-table-column>
            <el-table-column prop="programName" label="程序名" width="220" show-overflow-tooltip />
            <el-table-column prop="programVersion" label="程序版本" width="112"><template #default="{ row }"><b class="version-number">{{ row.programVersion }}</b></template></el-table-column>
            <el-table-column prop="versionDescription" label="程序版本描述" width="260" show-overflow-tooltip />
            <el-table-column prop="compiler" label="编译人" width="108" show-overflow-tooltip />
            <el-table-column prop="compileTime" label="编译时间" width="170"><template #default="{ row }">{{ formatDate(row.compileTime) }}</template></el-table-column>
            <el-table-column prop="compiledProgramVersion" label="编译程序版本" width="190" show-overflow-tooltip />
            <el-table-column prop="releaseTime" label="释放时间" width="170"><template #default="{ row }">{{ formatDate(row.releaseTime) }}</template></el-table-column>
            <el-table-column prop="branchName" label="关联分支名" width="220" show-overflow-tooltip />
            <el-table-column prop="remark1" label="备注1" width="180" show-overflow-tooltip />
            <el-table-column prop="remark2" label="备注2" width="180" show-overflow-tooltip />
            <el-table-column prop="createdBy" label="创建人" width="110" />
            <el-table-column prop="createdAt" label="创建时间" width="170"><template #default="{ row }">{{ formatDate(row.createdAt) }}</template></el-table-column>
            <el-table-column prop="updatedBy" label="最后修改人" width="120" />
            <el-table-column prop="updatedAt" label="最后修改时间" width="170"><template #default="{ row }">{{ formatDate(row.updatedAt) }}</template></el-table-column>
            <el-table-column label="删除标记" width="100"><template #default="{ row }"><span class="muted">{{ row.isDeleted ? '是' : '否' }}</span></template></el-table-column>
            <template #empty><div class="empty-table"><div class="empty-icon"><Document /></div><strong>暂无版本记录</strong><p>调整筛选条件或新增一条版本记录</p></div></template>
          </el-table>
          <div class="table-footer"><span>共 {{ total }} 条记录</span><el-pagination v-model:current-page="currentPage" v-model:page-size="pageSize" background layout="prev, pager, next, sizes" :total="total" :page-sizes="[10, 20, 50, 100]" @current-change="onPageChange" @size-change="currentPage = 1; fetchRecords()" /></div>
        </div>
      </section>

      <section v-else-if="activeView === 'logs'" class="page-body logs-page">
        <div class="panel filter-panel log-filter"><div class="filter-head"><div><h3>日志筛选</h3><p>操作日志只读，数据由后端事务统一写入</p></div><el-button text @click="resetLogFilters">重置条件</el-button></div><div class="filter-grid log-grid"><el-input v-model="logFilters.keyword" clearable placeholder="搜索操作人、业务 ID…" :prefix-icon="Search" @keyup.enter="logPage = 1; fetchLogs()" /><el-select v-model="logFilters.operationType" clearable placeholder="操作类型"><el-option label="新增" value="CREATE" /><el-option label="更新" value="UPDATE" /><el-option label="删除" value="DELETE" /><el-option label="恢复" value="RESTORE" /></el-select><el-date-picker v-model="logFilters.startTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" placeholder="开始时间" /><el-date-picker v-model="logFilters.endTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" placeholder="结束时间" /><el-button type="primary" :icon="Search" @click="logPage = 1; fetchLogs()">查询</el-button></div></div>
        <div class="panel log-panel"><div class="panel-toolbar"><div><h3>操作日志 <span>{{ logTotal }}</span></h3><p>包含修改前后 JSON、IP 地址及 User-Agent</p></div><el-tag type="info" effect="plain">只读审计</el-tag></div><el-table v-loading="pageLoading" :data="logs" class="log-table" stripe><el-table-column prop="operationTime" label="操作时间" width="172"><template #default="{ row }">{{ formatDate(row.operationTime) }}</template></el-table-column><el-table-column prop="operationType" label="操作类型" width="100" align="center"><template #default="{ row }"><el-tag :type="row.operationType === 'CREATE' || row.operationType === 'RESTORE' ? 'success' : row.operationType === 'DELETE' ? 'danger' : 'warning'">{{ row.operationType === 'CREATE' ? '新增' : row.operationType === 'DELETE' ? '删除' : row.operationType === 'RESTORE' ? '恢复' : '更新' }}</el-tag></template></el-table-column><el-table-column prop="operator" label="操作人员" width="120" /><el-table-column prop="businessId" label="业务记录 ID" width="250" show-overflow-tooltip /><el-table-column label="程序 / 版本" min-width="220"><template #default="{ row }"><b>{{ row.programName || '记录已删除' }}</b><span v-if="row.programVersion" class="log-version"> · {{ row.programVersion }}</span></template></el-table-column><el-table-column label="变更快照" min-width="250"><template #default="{ row }"><el-popover placement="left" width="500" trigger="click"><template #reference><el-button link type="primary">查看 JSON</el-button></template><div class="json-preview"><div><small>修改前</small><pre>{{ row.beforeData || '—' }}</pre></div><div><small>修改后</small><pre>{{ row.afterData || '—' }}</pre></div></div></el-popover></template></el-table-column><el-table-column prop="ipAddress" label="IP 地址" width="140" /><el-table-column prop="userAgent" label="浏览器" min-width="220" show-overflow-tooltip /><template #empty><div class="empty-table"><div class="empty-icon"><Tickets /></div><strong>暂无操作日志</strong><p>完成一次新增、更新、删除或恢复后，日志会显示在这里</p></div></template></el-table><div class="table-footer"><span>共 {{ logTotal }} 条日志</span><el-pagination v-model:current-page="logPage" v-model:page-size="logPageSize" background layout="prev, pager, next, sizes" :total="logTotal" :page-sizes="[10, 20, 50, 100]" @current-change="onLogPageChange" @size-change="logPage = 1; fetchLogs()" /></div></div>
      </section>

      <section v-else-if="activeView === 'personnel'" class="page-body personnel-page">
        <div class="panel personnel-panel">
          <div class="settings-toolbar personnel-toolbar">
            <div>
              <span class="settings-kicker">PERSONNEL MANAGEMENT</span>
              <h3>操作人员</h3>
              <p>人员管理是开发人和编译人下拉选项的唯一来源，新增/删除人员会同步更新下拉选项。</p>
            </div>
            <el-button type="primary" :icon="Plus" @click="openPersonnelCreate">新增操作人员</el-button>
          </div>
          <el-table v-loading="personnelLoading" :data="personnel" class="settings-table personnel-table" stripe row-key="id">
            <el-table-column type="index" label="#" width="56" align="center" />
            <el-table-column prop="employeeNumber" label="操作人员工号" width="120" />
            <el-table-column prop="personnelName" label="人员姓名" width="140">
              <template #default="{ row }"><strong class="option-value">{{ row.personnelName }}</strong></template>
            </el-table-column>
            <el-table-column prop="staffGroup" label="所属项目组" width="120">
              <template #default="{ row }"><span v-if="row.staffGroup">{{ row.staffGroup }}</span><span v-else class="muted">未分配</span></template>
            </el-table-column>
            <el-table-column prop="personnelGroup" label="所属人员组" width="120">
              <template #default="{ row }"><span v-if="row.personnelGroup">{{ row.personnelGroup }}</span><span v-else class="muted">未分配</span></template>
            </el-table-column>
            <el-table-column prop="createdAt" label="创建时间" width="160">
              <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
            </el-table-column>
            <el-table-column prop="updatedAt" label="最后更新时间" width="160">
              <template #default="{ row }">{{ formatDate(row.updatedAt) }}</template>
            </el-table-column>
            <el-table-column label="已分配权限" min-width="180">
              <template #default="{ row }">
                <div v-if="row.permissions.length" class="personnel-permissions">
                  <el-tag v-for="permission in row.permissions" :key="permission" size="small" effect="plain">
                    {{ permissionDefinitions.find((item) => item.code === permission)?.label || permission }}
                  </el-tag>
                </div>
                <span v-else class="muted">暂未分配</span>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="280" fixed="right" align="center">
              <template #default="{ row }">
                <div class="personnel-actions">
                  <el-button link type="primary" :icon="EditPen" @click="openPersonnelEdit(row)">编辑</el-button>
                  <el-button v-if="isAdmin" link type="primary" :icon="Setting" @click="openPersonnelPermissions(row)">权限设置</el-button>
                  <el-button link type="warning" :icon="Lock" @click="openResetPersonnelPassword(row)">重置密码</el-button>
                  <el-button link type="danger" :icon="Delete" @click="deletePersonnel(row)">删除</el-button>
                </div>
              </template>
            </el-table-column>
            <template #empty>
              <div class="empty-table settings-empty">
                <div class="empty-icon"><User /></div>
                <strong>暂无操作人员</strong>
                <p>点击“新增操作人员”创建可登录系统的人员账号</p>
              </div>
            </template>
          </el-table>
          <div class="settings-footer">当前共 <b>{{ personnel.length }}</b> 名操作人员</div>
        </div>
      </section>

      <section v-else-if="activeView === 'settings'" class="page-body settings-page">
        <div class="settings-layout">
          <aside class="panel settings-categories">
            <div class="settings-category-head">
              <span class="action-icon"><Setting /></span>
              <div><h3>选项分类</h3><p>选择需要维护的内容类型</p></div>
            </div>
            <button
              v-for="definition in visibleOptionTypeDefinitions"
              :key="definition.type"
              class="settings-category"
              :class="{ active: selectedOptionType === definition.type }"
              @click="selectOptionType(definition.type)"
            >
              <span><strong>{{ definition.label }}</strong><small>{{ definition.description }}</small></span>
              <b>{{ optionCount(definition.type) }}</b>
            </button>
          </aside>

          <div class="panel settings-panel">
            <div class="settings-toolbar">
              <div class="settings-toolbar-title">
                <span class="settings-kicker">PROJECT OPTIONS</span>
                <h3>{{ currentOptionDefinition.label }}</h3>
                <span class="settings-toolbar-desc">{{ currentOptionDefinition.description }}</span>
              </div>
              <div class="settings-actions">
                <el-select
                  v-if="canFilterByStaffGroup"
                  v-model="optionStaffGroupFilter"
                  clearable
                  filterable
                  placeholder="筛选项目组"
                  class="option-staff-filter"
                >
                  <el-option label="全部项目组" value="" />
                  <el-option v-for="item in settingStaffGroups" :key="item" :label="item" :value="item" />
                </el-select>
                <el-select
                  v-if="canFilterByPersonnelGroup"
                  v-model="optionPersonnelGroupFilter"
                  clearable
                  filterable
                  placeholder="筛选人员组"
                  class="option-staff-filter"
                >
                  <el-option label="全部人员组" value="" />
                  <el-option v-for="item in settingPersonnelGroups" :key="item" :label="item" :value="item" />
                </el-select>
                <el-select
                  v-if="canFilterByProgramGroup"
                  v-model="optionProgramGroupFilter"
                  clearable
                  filterable
                  placeholder="筛选专案名称"
                  class="option-staff-filter"
                >
                  <el-option label="全部专案名称" value="" />
                  <el-option v-for="item in settingProgramGroups" :key="item" :label="item" :value="item" />
                </el-select>
                <el-input
                  v-model="optionKeywordFilter"
                  clearable
                  :prefix-icon="Search"
                  placeholder="模糊搜索"
                  class="option-keyword-filter"
                />
                <el-button :icon="Upload" size="small" @click="openOptionImport">批量导入</el-button>
                <el-button :icon="EditPen" size="small" :disabled="selectedProjectOptions.length !== 1" @click="editSelectedProjectOption">修改</el-button>
                <el-button type="danger" plain size="small" :icon="Delete" :disabled="selectedProjectOptions.length !== 1" @click="deleteSelectedProjectOption">删除</el-button>
                <el-button type="primary" size="small" :icon="Plus" @click="openOptionCreate">新增选项</el-button>
              </div>
            </div>
            <el-table v-loading="optionLoading" :data="filteredProjectOptions" class="settings-table" stripe row-key="id" :row-class-name="optionRowClassName" @selection-change="handleProjectOptionSelection">
              <el-table-column type="selection" width="48" align="center" />
              <el-table-column type="index" label="#" width="52" align="center" />
              <el-table-column prop="optionValue" label="选项内容" min-width="240" show-overflow-tooltip>
                <template #default="{ row }"><strong class="option-value">{{ row.optionValue }}</strong></template>
              </el-table-column>
              <el-table-column v-if="selectedOptionType === 'PERSONNEL_GROUP'" prop="staffGroup" label="所属项目组" width="130" show-overflow-tooltip>
                <template #default="{ row }">{{ row.staffGroup || '未关联' }}</template>
              </el-table-column>
              <el-table-column v-if="selectedOptionType === 'PROGRAM_GROUP'" prop="staffGroup" label="所属项目组" width="130" show-overflow-tooltip>
                <template #default="{ row }">{{ row.staffGroup || '未关联' }}</template>
              </el-table-column>
              <el-table-column v-if="selectedOptionType === 'PROGRAM_GROUP'" prop="personnelGroup" label="所属人员组" width="130" show-overflow-tooltip>
                <template #default="{ row }">{{ row.personnelGroup || '未关联' }}</template>
              </el-table-column>
              <el-table-column v-if="selectedOptionType === 'PROGRAM_NAME'" prop="programGroup" label="所属专案名称" width="130" show-overflow-tooltip>
                <template #default="{ row }">{{ row.programGroup || '未关联' }}</template>
              </el-table-column>
              <el-table-column v-if="selectedOptionType === 'PROGRAM_NAME'" prop="personnelGroup" label="所属人员组" width="130" show-overflow-tooltip>
                <template #default="{ row }">{{ row.personnelGroup || '未关联' }}</template>
              </el-table-column>
              <el-table-column v-if="selectedOptionType === 'PROGRAM_NAME'" prop="staffGroup" label="所属项目组" width="130" show-overflow-tooltip>
                <template #default="{ row }">{{ row.staffGroup || '未关联' }}</template>
              </el-table-column>
              <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
              <el-table-column prop="updatedBy" label="最后修改人" width="120" show-overflow-tooltip />
              <el-table-column prop="updatedAt" label="最后修改时间" width="160" show-overflow-tooltip>
                <template #default="{ row }">{{ formatDate(row.updatedAt) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="150" fixed="right" align="center">
                <template #default="{ row }">
                  <el-button link type="primary" :icon="EditPen" @click="openOptionEdit(row)">编辑</el-button>
                  <el-button link type="danger" :icon="Delete" @click="deleteProjectOption(row)">删除</el-button>
                </template>
              </el-table-column>
              <template #empty>
                <div class="empty-table settings-empty">
                  <div class="empty-icon"><Setting /></div>
                  <strong>暂无{{ currentOptionDefinition.label }}选项</strong>
                  <p>点击“新增选项”后即可在版本记录表单中选择</p>
                </div>
              </template>
            </el-table>
            <div class="settings-footer">
              当前分类共 <b>{{ filteredProjectOptions.length }}</b> 个选项
              <span v-if="selectedProjectOptions.length">，已选择 <b>{{ selectedProjectOptions.length }}</b> 个</span>
            </div>
          </div>
        </div>
      </section>

      <section v-else class="page-body no-access-page">
        <div class="panel no-access-card">
          <div class="empty-icon"><Lock /></div>
          <h3>当前账号暂未分配功能权限</h3>
          <p>请联系系统管理员，在“人员管理 → 权限设置”中分配所需权限，然后重新登录。</p>
        </div>
      </section>
    </main>
  </div>

  <el-dialog v-model="changePasswordDialogVisible" title="修改登录密码" width="480px" destroy-on-close>
    <el-form label-position="top" class="option-form" @submit.prevent>
      <el-form-item label="当前密码">
        <el-input v-model="changePasswordForm.currentPassword" type="password" show-password maxlength="200" autocomplete="current-password" placeholder="请输入当前登录密码" />
      </el-form-item>
      <el-form-item label="新密码">
        <el-input v-model="changePasswordForm.newPassword" type="password" show-password maxlength="100" autocomplete="new-password" placeholder="至少 6 位" />
      </el-form-item>
      <el-form-item label="确认新密码">
        <el-input v-model="changePasswordForm.confirmPassword" type="password" show-password maxlength="100" autocomplete="new-password" placeholder="请再次输入新密码" @keyup.enter="submitChangePassword" />
      </el-form-item>
      <div class="option-form-tip">密码修改成功后，系统会退出当前登录，请使用新密码重新登录。</div>
    </el-form>
    <template #footer>
      <el-button :disabled="changePasswordLoading" @click="changePasswordDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="changePasswordLoading" @click="submitChangePassword">确认修改</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="personnelDialogVisible" :title="personnelDialogMode === 'create' ? '新增操作人员' : '编辑人员信息'" width="540px" destroy-on-close>
    <el-form ref="personnelFormRef" :model="personnelForm" :rules="personnelFormRules" label-position="top" class="option-form">
      <el-form-item label="操作人员工号" prop="employeeNumber">
        <el-input v-model="personnelForm.employeeNumber" maxlength="100" placeholder="请输入登录工号，如 10086" />
      </el-form-item>
      <el-form-item label="人员姓名" prop="personnelName">
        <el-input v-model="personnelForm.personnelName" maxlength="100" placeholder="请输入真实姓名" />
      </el-form-item>
      <el-form-item label="所属项目组" prop="staffGroup">
        <el-select v-model="personnelForm.staffGroup" filterable clearable class="full" placeholder="请选择该人员所属的项目组" :disabled="!isAdmin" @change="onPersonnelStaffGroupChange">
          <el-option v-if="isAdmin" label="未分配" value="" />
          <el-option v-for="item in settingStaffGroups" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">设置项目组后，该人员登录后只能查看和操作所属项目组的版本记录。</div>
      </el-form-item>
      <el-form-item label="所属人员组" prop="personnelGroup">
        <el-select v-model="personnelForm.personnelGroup" filterable class="full" placeholder="请选择该人员所属的人员组">
          <el-option v-for="item in filteredPersonnelGroupsForPersonnel" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">人员组是项目组的下一级分组。若人员组名称与项目组名称相同，则该人员不受人员组限制，可查看项目组下所有记录。</div>
      </el-form-item>
      <el-form-item v-if="personnelDialogMode === 'create'" label="管理密码" prop="managementPassword">
        <el-input v-model="personnelForm.managementPassword" type="password" show-password maxlength="100" placeholder="至少 6 位，用于人员登录系统" />
      </el-form-item>
      <div class="option-form-tip">保存后，人员姓名会立即出现在开发人和编译人下拉选项中。</div>
    </el-form>
    <template #footer>
      <el-button @click="personnelDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="personnelSaving" @click="savePersonnel">保存人员</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="resetPasswordDialogVisible" title="重置管理密码" width="480px" destroy-on-close>
    <el-form label-position="top" class="option-form">
      <el-form-item label="操作人员">
        <el-input :model-value="resetPasswordName" disabled />
      </el-form-item>
      <el-form-item label="新管理密码">
        <el-input v-model="resetPassword" type="password" show-password maxlength="100" placeholder="请输入至少 6 位的新密码" @keyup.enter="submitResetPersonnelPassword" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="resetPasswordDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="resetPasswordLoading" @click="submitResetPersonnelPassword">确认重置</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="permissionDialogVisible" title="修改人员权限" width="680px" destroy-on-close>
    <div class="permission-dialog-head">
      <strong>{{ permissionPersonnelName }}</strong>
      <span>勾选该人员可以使用的功能。权限修改后，用户需要重新登录才会生效。</span>
    </div>
    <el-checkbox-group v-model="selectedPermissions" class="permission-grid">
      <el-checkbox v-for="definition in permissionDefinitions" :key="definition.code" :value="definition.code" border>
        <span class="permission-option">
          <strong>{{ definition.label }}</strong>
          <small>{{ definition.description }}</small>
        </span>
      </el-checkbox>
    </el-checkbox-group>
    <template #footer>
      <el-button @click="permissionDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="permissionSaving" @click="savePersonnelPermissions">保存权限</el-button>
    </template>
  </el-dialog>

  <el-drawer v-model="drawerVisible" :title="drawerTitle" size="640px" destroy-on-close>
    <el-form ref="formRef" :model="form" :rules="formRules" label-position="top" class="version-form">
      <div class="form-section-title">基础信息 <span>必填字段</span></div>
      <div class="form-grid two">
        <el-form-item label="项目时间" prop="projectDate"><el-date-picker v-model="form.projectDate" type="date" value-format="YYYY-MM-DD" class="full" /></el-form-item>
        <el-form-item label="项目类型" prop="projectType"><el-select v-model="form.projectType" filterable class="full" placeholder="请选择项目类型"><el-option v-for="item in withCurrentOption(options.projectTypes, form.projectType)" :key="item" :label="item" :value="item" /></el-select></el-form-item>
        <el-form-item label="开发人" prop="developer"><el-select v-model="form.developer" filterable class="full" placeholder="请选择开发人"><el-option v-for="item in withCurrentOption(filteredDevelopers, form.developer)" :key="item" :label="item" :value="item" /></el-select></el-form-item>
        <el-form-item label="专案名称" prop="programGroup"><el-select v-model="formProgramGroupKey" filterable class="full" :disabled="!form.developer" :placeholder="form.developer ? '请选择专案名称' : '请先选择开发人'" @change="onProgramGroupChange"><el-option v-for="item in formProgramGroupOptionsWithCurrent" :key="item.value" :label="item.label" :value="item.value" /></el-select></el-form-item>
        <el-form-item label="程序名" prop="programName"><el-select v-model="form.programName" filterable class="full" :disabled="!form.programGroup" :placeholder="form.programGroup ? '请选择程序名' : '请先选择专案名称'"><el-option v-for="item in withCurrentOption(options.programNames, form.programName)" :key="item" :label="item" :value="item" /></el-select></el-form-item>
        <el-form-item label="程序版本" prop="programVersion"><el-input v-model="form.programVersion" placeholder="如 1.0.0" /></el-form-item>
      </div>
      <div class="form-section-title">编译与发布</div>
      <div class="form-grid two"><el-form-item label="编译人"><el-select v-model="form.compiler" clearable filterable class="full" placeholder="请选择编译人"><el-option v-for="item in withCurrentOption(options.compilers, form.compiler)" :key="item" :label="item" :value="item" /></el-select></el-form-item><el-form-item label="编译时间"><el-date-picker v-model="form.compileTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" class="full" /></el-form-item><el-form-item label="编译程序版本"><el-input v-model="form.compiledProgramVersion" /></el-form-item><el-form-item label="释放时间"><el-date-picker v-model="form.releaseTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ss" class="full" /></el-form-item><el-form-item label="状态"><el-select v-model="form.status" class="full"><el-option label="待释放" value="PENDING" /><el-option label="待编译" value="WAIT_COMPILE" /><el-option label="已编译" value="COMPILED" /><el-option label="提前释放" value="PRE_RELEASED" /><el-option label="已释放" value="RELEASED" /><el-option label="已取消" value="CANCELED" /><el-option label="已注释" value="COMMENTED" /></el-select></el-form-item><el-form-item label="关联分支名"><el-input v-model="form.branchName" /></el-form-item></div>
      <el-form-item label="程序版本描述"><el-input v-model="form.versionDescription" type="textarea" :rows="3" maxlength="2000" show-word-limit /></el-form-item>
      <div class="form-section-title">备注</div>
      <div class="form-grid two"><el-form-item label="备注1"><el-input v-model="form.remark1" type="textarea" :rows="2" /></el-form-item><el-form-item label="备注2"><el-input v-model="form.remark2" type="textarea" :rows="2" /></el-form-item></div>
      <div class="drawer-footer"><el-button :disabled="recordSaving" @click="drawerVisible = false">取消</el-button><el-button v-if="editingId ? hasPermission('VERSION_UPDATE') : hasPermission('VERSION_CREATE')" type="primary" :loading="recordSaving" @click="saveRecord">保存记录</el-button></div>
    </el-form>
  </el-drawer>

  <el-dialog v-model="optionDialogVisible" :title="optionDialogTitle" width="520px" destroy-on-close>
    <el-form ref="optionFormRef" :model="optionForm" :rules="optionFormRules" label-position="top" class="option-form">
      <el-form-item label="选项分类" prop="optionType">
        <el-select v-model="optionForm.optionType" class="full" disabled>
          <el-option v-for="definition in visibleOptionTypeDefinitions" :key="definition.type" :label="definition.label" :value="definition.type" />
        </el-select>
        <div class="option-form-tip">当前分类由左侧选项分类锁定，避免误添加到其他项目。</div>
      </el-form-item>
      <el-form-item label="选项内容" prop="optionValue">
        <el-input v-model="optionForm.optionValue" maxlength="200" show-word-limit placeholder="请输入需要在版本表单中显示的内容" />
      </el-form-item>
      <el-form-item v-if="optionForm.optionType === 'PERSONNEL_GROUP'" label="所属项目组" prop="staffGroup">
        <el-select v-model="optionForm.staffGroup" filterable class="full" placeholder="请选择该人员组所属的项目组" :disabled="!isAdmin" @change="onOptionStaffGroupChange">
          <el-option v-for="item in settingStaffGroups" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">人员组必须属于一个项目组，便于按项目组筛选。</div>
      </el-form-item>
      <el-form-item v-if="optionForm.optionType === 'PROGRAM_GROUP'" label="所属项目组" prop="staffGroup">
        <el-select v-model="optionForm.staffGroup" filterable class="full" placeholder="请选择该专案所属的项目组" :disabled="!isAdmin" @change="onOptionStaffGroupChange">
          <el-option v-for="item in settingStaffGroups" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">专案可选择所属的项目组，便于版本录入时按组筛选。</div>
      </el-form-item>
      <el-form-item v-if="optionForm.optionType === 'PROGRAM_GROUP'" label="所属人员组" prop="personnelGroup">
        <el-select v-model="optionForm.personnelGroup" filterable class="full" placeholder="请选择该专案所属的人员组" :disabled="isOptionPersonnelGroupLocked" @change="onOptionPersonnelGroupChange">
          <el-option v-for="item in filteredSettingPersonnelGroups" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">专案必须指定所属人员组，便于版本录入时按人员组筛选。</div>
      </el-form-item>
      <el-form-item v-if="optionForm.optionType === 'PROGRAM_NAME'" label="所属专案名称" prop="programGroup">
        <el-select v-model="optionForm.programGroup" filterable class="full" placeholder="请选择该程序所属的专案名称" @change="onOptionProgramGroupChange">
          <el-option v-for="item in filteredSettingProgramGroupsForOption" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">新增版本时，程序名会根据这里设置的专案自动筛选。</div>
      </el-form-item>
      <el-form-item v-if="optionForm.optionType === 'PROGRAM_NAME'" label="所属人员组">
        <el-select v-model="optionForm.personnelGroup" filterable clearable class="full" placeholder="请选择该程序所属的人员组" :disabled="isOptionPersonnelGroupLocked">
          <el-option v-for="item in filteredSettingPersonnelGroups" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">默认从当前用户人员组加载，可手动修改。</div>
      </el-form-item>
      <el-form-item v-if="optionForm.optionType === 'PROGRAM_NAME'" label="所属项目组" prop="staffGroup">
        <el-select v-model="optionForm.staffGroup" filterable clearable class="full" placeholder="请选择该程序所属的项目组（可选）" :disabled="!isAdmin" @change="onOptionStaffGroupChange">
          <el-option v-for="item in settingStaffGroups" :key="item" :label="item" :value="item" />
        </el-select>
        <div class="option-form-tip">程序名可关联所属的项目组，便于版本录入时按项目组筛选。</div>
      </el-form-item>
      <el-form-item label="排序值" prop="sortOrder">
        <el-input-number v-model="optionForm.sortOrder" :min="0" :max="999999" :step="10" controls-position="right" class="full" />
        <div class="option-form-tip">排序值越小，下拉列表中的位置越靠前。</div>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="optionDialogVisible = false">取消</el-button>
      <el-button type="primary" @click="saveProjectOption">保存选项</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="versionImportDialogVisible" title="批量导入项目版本记录" width="660px" destroy-on-close>
    <div class="option-import-guide">
      <div class="import-guide-heading">
        <h4>请先下载并填写系统模板</h4>
        <el-button :icon="Download" :loading="versionTemplateLoading" @click="downloadVersionImportTemplate">下载导入模板</el-button>
      </div>
      <p>模板包含字段说明和当前“项目设置”中的可选项参考，请不要修改第一行标题。</p>
      <p>项目时间、项目类型、开发人、专案名称、程序名、程序版本、状态为必填项。</p>
      <p>任意一行格式错误会阻止整批导入；相同专案名称、程序名和程序版本的记录会自动跳过。</p>
    </div>
    <label class="option-import-picker">
      <input :key="versionImportInputKey" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" @change="handleVersionImportFile" />
      <span class="option-import-icon"><Upload /></span>
      <strong>{{ versionImportFile ? versionImportFile.name : '选择填写完成的 Excel 文件' }}</strong>
      <small>{{ versionImportFile ? `${(versionImportFile.size / 1024).toFixed(1)} KB` : '仅支持 .xlsx，文件最大 5 MB，单次最多 1000 行' }}</small>
    </label>
    <template #footer>
      <el-button :disabled="versionImportLoading" @click="versionImportDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="versionImportLoading" :disabled="!versionImportFile" @click="importProjectVersions">开始导入</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="optionImportDialogVisible" title="批量导入项目选项" width="620px" destroy-on-close>
    <div class="option-import-guide">
      <h4>Excel 格式要求</h4>
      <p>仅支持 .xlsx，第一行依次为：<b>选项分类</b>、<b>选项内容</b>、<b>排序</b>、<b>所属专案</b>、<b>所属项目组</b>、<b>所属人员组</b>。</p>
      <p><b>专案名称</b>：所属项目组和所属人员组均为必填，所属专案留空。须先在项目设置中维护对应的项目组和人员组。</p>
      <p><b>程序名</b>：所属专案为必填，所属项目组和所属人员组可选（需与所属专案一致）。其他分类不填所属专案。</p>
      <p><b>所属项目组</b>：开发人、编译人、人员组、专案、程序名可填写；项目类型留空。须已存在或先于本批导入。</p>
      <p>排序留空时按行号自动生成。文件中的错误会阻止整批导入；与现有数据或文件内重复的选项会自动跳过。</p>
      <p v-if="!hasPermission('OPTION_MANAGE')">当前账号只能导入「专案名称」和「程序名」两类选项。</p>
    </div>
    <div class="option-import-actions">
      <el-button :icon="Download" :loading="optionTemplateLoading" @click="downloadOptionImportTemplate('PROGRAM_GROUP')">下载专案名称模板</el-button>
      <el-button :icon="Download" :loading="optionTemplateLoading" @click="downloadOptionImportTemplate()">下载通用模板</el-button>
    </div>
    <label class="option-import-picker">
      <input :key="optionImportInputKey" type="file" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" @change="handleOptionImportFile" />
      <span class="option-import-icon"><Upload /></span>
      <strong>{{ optionImportFile ? optionImportFile.name : '选择 Excel 文件' }}</strong>
      <small>{{ optionImportFile ? `${(optionImportFile.size / 1024).toFixed(1)} KB` : '文件最大 5 MB，单次最多 1000 行' }}</small>
    </label>
    <template #footer>
      <el-button :disabled="optionImportLoading" @click="optionImportDialogVisible = false">取消</el-button>
      <el-button type="primary" :loading="optionImportLoading" :disabled="!optionImportFile" @click="importProjectOptions">开始导入</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="detailVisible" title="版本详情" width="760px" destroy-on-close>
    <template v-if="detailRecord">
      <div class="detail-hero">
        <div class="detail-status"><el-tag :type="statusType(detailRecord.status)" size="large">{{ statusLabel(detailRecord.status) }}</el-tag><span>{{ detailRecord.isDeleted ? '已删除记录' : '有效记录' }}</span></div>
        <div class="detail-title-stack">
          <span>{{ detailRecord.programGroup }}</span>
          <h3>{{ detailRecord.programName }}</h3>
          <p>{{ detailRecord.programVersion }}</p>
        </div>
      </div>
      <div class="detail-grid">
        <div><small>项目类型</small><strong>{{ detailRecord.projectType }}</strong></div>
        <div><small>开发人</small><strong>{{ detailRecord.developer }}</strong></div>
        <div><small>项目时间</small><strong>{{ dateOnly(detailRecord.projectDate) }}</strong></div>
        <div><small>关联分支</small><strong>{{ detailRecord.branchName || '—' }}</strong></div>
        <div><small>编译人</small><strong>{{ detailRecord.compiler || '—' }}</strong></div>
        <div><small>编译程序版本</small><strong>{{ detailRecord.compiledProgramVersion || '—' }}</strong></div>
        <div><small>编译时间</small><strong>{{ formatDate(detailRecord.compileTime) }}</strong></div>
        <div><small>释放时间</small><strong>{{ formatDate(detailRecord.releaseTime) }}</strong></div>
        <div><small>最后修改</small><strong>{{ detailRecord.updatedBy }} · {{ formatDate(detailRecord.updatedAt) }}</strong></div>
      </div>
      <div class="detail-description"><small>程序版本描述</small><p>{{ detailRecord.versionDescription || '暂无描述' }}</p></div>
      <div class="detail-remarks">
        <div><small>备注1</small><p>{{ detailRecord.remark1 || '—' }}</p></div>
        <div><small>备注2</small><p>{{ detailRecord.remark2 || '—' }}</p></div>
      </div>
    </template>
    <template #footer>
      <div class="detail-footer-actions">
        <el-button v-if="detailRecord && !detailRecord.isDeleted && hasPermission('VERSION_UPDATE')" :icon="EditPen" @click="updateDetailRecord">更新记录</el-button>
        <el-button v-if="detailRecord && !detailRecord.isDeleted && hasPermission('VERSION_DELETE')" type="danger" plain :icon="Delete" @click="deleteDetailRecord">删除记录</el-button>
        <el-button v-if="detailRecord && isAdmin" type="danger" :icon="Delete" @click="permanentlyDeleteDetailRecord">管理员物理删除</el-button>
      </div>
    </template>
  </el-dialog>
</template>
