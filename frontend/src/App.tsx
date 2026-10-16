import { useCallback, useEffect, useState } from 'react'
import { apiFetch, workspaceStorageKey } from './lib/api'
import { useApiQuery } from './hooks/useApiQuery'
import {
  AlertTriangle, ArrowDownLeft, ArrowLeftRight, ArrowRight, ArrowUpRight, Ban, BarChart3, Bell, Building2,
  BookOpen, CalendarDays, ChevronDown, ChevronLeft, ChevronRight, CircleDollarSign,
  CheckCircle2, Clock3, Download, Eye, EyeOff, FileBarChart, FileUp, Goal, HelpCircle, History, Landmark, LayoutDashboard, Link2, LockKeyhole, LogOut, Mail, Menu,
  Headphones, MessageCircle, MoreHorizontal, Paperclip, Pencil, PieChart, Plus, Printer, ReceiptText, Repeat2, RotateCcw, Search, Send, Settings, SlidersHorizontal, UserRound,
  RefreshCw, Server, ShieldCheck, Sparkles, Tags, TrendingDown, TrendingUp, UsersRound, WalletCards, Wrench, X,
} from 'lucide-react'
import QRCode from 'qrcode'
import './App.css'

function financialDate(date = new Date()) {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(date)
  const part = (type: string) => parts.find(item => item.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}`
}
type NavItem = { id: string; label: string; icon: typeof LayoutDashboard; badge?: number }
type GlobalRole = 'User' | 'Support' | 'Developer' | 'SuperAdmin'
type AuthUser = { id: string; name: string; email: string; globalRole: GlobalRole; isTwoFactorEnabled: boolean }
type ReminderDays = 1 | 3 | 7 | 15
type AppPreferences = { hideValuesByDefault: boolean; compactMode: boolean; remindersEnabled: boolean; reminderDays: ReminderDays }
type WorkspaceData = { id: string; name: string; kind: 'Personal' | 'Business'; role: 'Owner' | 'Administrator' | 'Member'; createdAt: string }
type Transaction = { id?: string; accountId?: string; name: string; detail: string; category: string; type: 'Income' | 'Expense'; value: number; occurredOn: string; icon: string; tone: string; transferId?: string; counterpartyAccountId?: string; counterpartyAccountName?: string; isTransfer?: boolean }
type Account = { id: string; name: string; institution: string; lastFourDigits?: string; type: 'Checking' | 'Savings' | 'Cash' | 'Investment'; openingBalance: number; balance: number; isActive: boolean; transactionCount: number }
type DashboardData = { totalBalance: number; monthIncome: number; monthExpense: number; monthResult: number; accounts: Account[]; recentTransactions: ApiTransaction[] }
type ApiTransaction = { id: string; accountId: string; accountName: string; description: string; category: string; type: string; amount: number; signedAmount: number; occurredOn: string; createdAt: string; transferId?: string; counterpartyAccountId?: string; counterpartyAccountName?: string }
type CashFlowData = { startDate: string; endDate: string; openingBalance: number; currentBalance: number; projectedBalance: number; totalIncome: number; totalExpense: number; projectedIncome: number; projectedExpense: number; netResult: number; points: CashFlowPoint[] }
type CashFlowPoint = { date: string; income: number; expense: number; projectedIncome: number; projectedExpense: number; balance: number; isProjected: boolean }
type CommitmentData = { id: string; seriesId: string; contactId?: string; description: string; contact: string; category: string; kind: 'Payable' | 'Receivable'; totalAmount: number; settledAmount: number; remainingAmount: number; dueDate: string; competenceDate: string; installmentNumber: number; installmentCount: number; status: 'Open' | 'PartiallySettled' | 'Settled' | 'Cancelled'; isOverdue: boolean; recurrenceFrequency?: 'Weekly' | 'Monthly' | 'Yearly'; recurrenceNumber: number; recurrenceCount: number }
type ReconciliationSuggestion = { transactionId: string; description: string; category: string; occurredOn: string; signedAmount: number; dateDistanceDays: number; score: number; confidence: 'High' | 'Medium' | 'Low' }
type ReconciliationCategorySuggestion = { ruleName: string; category: string; costCenterId?: string; costCenterName?: string }
type BankEntry = { id: string; accountId: string; accountName: string; description: string; type: 'Income' | 'Expense'; amount: number; signedAmount: number; occurredOn: string; status: 'Pending' | 'Reconciled' | 'Ignored'; transactionId?: string; transactionWasCreated: boolean; suggestion?: ReconciliationSuggestion; categorySuggestion?: ReconciliationCategorySuggestion }
type ReconciliationData = { total: number; pending: number; reconciled: number; ignored: number; importedBalance: number; systemBalance: number; difference: number; entries: BankEntry[] }
type ReconciliationHistoryData = { id: string; bankStatementEntryId: string; accountId: string; accountName: string; userId: string; userName: string; action: string; transactionId?: string; transactionWasCreated: boolean; transactionRemoved: boolean; description: string; category?: string; costCenterName?: string; ruleName?: string; details?: string; occurredAt: string }
type BankImportMatchSuggestion = { transactionId: string; description: string; category: string; occurredOn: string; signedAmount: number; dateDistanceDays: number; score: number; confidence: 'High' | 'Medium' | 'Low' }
type BankImportCategorySuggestion = { ruleId: string; ruleName: string; category: string; costCenterId?: string; costCenterName?: string }
type BankImportPreviewRow = { rowNumber: number; externalId?: string; description: string; occurredOn?: string; signedAmount?: number; status: 'Ready' | 'Duplicate' | 'Invalid'; issue?: string; matchSuggestion?: BankImportMatchSuggestion; categorySuggestion?: BankImportCategorySuggestion }
type BankImportPreviewData = { fileName: string; format: string; total: number; ready: number; duplicates: number; invalid: number; totalIncome: number; totalExpense: number; rows: BankImportPreviewRow[] }
type BankImportDecision = { rowNumber: number; action: 'Match' | 'Create' | 'Ignore' | 'Pending'; transactionId?: string; category?: string; costCenterId?: string; saveAsRule?: boolean; rulePattern?: string }
type CategoryData = { id: string; name: string; kind: 'Income' | 'Expense' | 'Both'; parentId?: string; parentName?: string; isActive: boolean }
type CostCenterData = { id: string; name: string; code: string; isActive: boolean; allocatedAmount: number }
type CategorizationRuleData = { id: string; name: string; descriptionPattern: string; matchMode: 'Contains' | 'StartsWith' | 'Equals'; transactionType?: 'Income' | 'Expense'; accountId?: string; accountName?: string; category: string; costCenterId?: string; costCenterName?: string; priority: number; isActive: boolean }
type CategorizationPreviewData = { matchCount: number; samples: ApiTransaction[] }
type AttachmentData = { id: string; entityType: 'Transaction' | 'Commitment'; entityId: string; fileName: string; contentType: string; size: number; uploadedByUserId: string; uploadedAt: string }
type AllocationData = { id: string; transactionId: string; costCenterId: string; costCenterName: string; costCenterCode: string; amount: number; percentage: number }
type BudgetLineData = { id: string; targetKind: 'Category' | 'CostCenter'; targetId: string; targetName: string; targetCode?: string; plannedAmount: number; actualAmount: number; projectedAmount: number; availableAmount: number; usagePercentage: number; alertPercentage: number; status: 'Healthy' | 'Warning' | 'Exceeded' }
type BudgetSummaryData = { year: number; month: number; plannedAmount: number; actualAmount: number; projectedAmount: number; availableAmount: number; alertCount: number; lines: BudgetLineData[] }
type DreLineData = { category: string; kind: 'Income' | 'Expense'; currentAmount: number; previousAmount: number; variationPercentage: number }
type DreData = { year: number; month: number; costCenterId?: string; costCenterName?: string; grossRevenue: number; operatingExpenses: number; netResult: number; marginPercentage: number; previousGrossRevenue: number; previousOperatingExpenses: number; previousNetResult: number; resultVariationPercentage: number; revenueLines: DreLineData[]; expenseLines: DreLineData[] }
type BudgetVarianceLineData = { budgetId: string; targetKind: 'Category' | 'CostCenter'; targetId: string; targetName: string; targetCode?: string; plannedAmount: number; actualAmount: number; committedAmount: number; projectedAmount: number; deviationAmount: number; deviationPercentage: number; alertPercentage: number; status: 'Healthy' | 'Warning' | 'Exceeded' }
type BudgetPeriodData = { year: number; month: number; plannedAmount: number; actualAmount: number; committedAmount: number; projectedAmount: number; deviationAmount: number; alertCount: number }
type BudgetVarianceData = { year: number; month: number; plannedAmount: number; actualAmount: number; committedAmount: number; projectedAmount: number; deviationAmount: number; executionPercentage: number; alertCount: number; lines: BudgetVarianceLineData[]; periods: BudgetPeriodData[] }
type GoalContributionData = { id: string; amount: number; contributedOn: string; note: string }
type FinancialGoalData = { id: string; name: string; kind: 'Revenue' | 'ExpenseLimit' | 'EmergencyReserve' | 'CashBalance' | 'Custom'; targetAmount: number; currentAmount: number; remainingAmount: number; progressPercentage: number; startDate: string; dueDate: string; projectedCompletionDate?: string; category?: string; status: 'OnTrack' | 'AtRisk' | 'Overdue' | 'Exceeded' | 'Completed' | 'Archived'; isActive: boolean; contributions: GoalContributionData[] }
type GoalsSummaryData = { activeCount: number; completedCount: number; atRiskCount: number; totalTargetAmount: number; totalCurrentAmount: number; goals: FinancialGoalData[] }
type ContactData = { id: string; name: string; kind: 'Customer' | 'Supplier' | 'Both'; taxId?: string; email?: string; phone?: string; isActive: boolean; commitmentCount: number }
type AuditData = { id: string; action: string; entityType: string; entityId: string; description: string; details?: string; userId: string; occurredAt: string }
type PlatformOverviewData = { userCount: number; activeUserCount: number; workspaceCount: number; superAdminCount: number; developerCount: number; auditEventCount: number }
type PlatformUserData = { id: string; name: string; email: string; globalRole: GlobalRole; isActive: boolean; workspaceCount: number; createdAt: string }
type PlatformWorkspaceData = { id: string; name: string; kind: string; memberCount: number; createdAt: string }
type PlatformAuditData = { id: string; actorUserId: string; actorName: string; action: string; entityType: string; entityId?: string; description: string; details?: string; ipAddress?: string; occurredAt: string }
type PlatformDiagnosticData = { status: string; environment: string; version: string; databaseProvider: string; appliedMigrations: number; pendingMigrations: number; serverTimeUtc: string }
type PlatformSettingData = { key: string; value: string; updatedByUserId: string; updatedAt: string }
type WorkspaceMemberData = { userId: string; name: string; email: string; role: 'Owner' | 'Administrator' | 'Member'; joinedAt: string }
type WorkspaceInvitationData = { id: string; workspaceId: string; workspaceName: string; email: string; role: 'Administrator' | 'Member'; status: string; expiresAt: string; createdAt: string }
type FinancialInsight = { id: string; title: string; message: string; actionLabel: string; target: string; tone: 'positive' | 'attention' | 'critical' }
type HelpArticleData = { id: string; title: string; summary: string; category: string; module: string; keywords: string[]; steps: string[] }
type SupportTicketMessageData = { id: string; authorUserId: string; authorName: string; body: string; createdAt: string }
type SupportTicketData = { id: string; workspaceId: string; workspaceName: string; requesterUserId: string; requesterName: string; target: 'WorkspaceAdministration' | 'Support' | 'Development'; subject: string; category: string; priority: 'Low' | 'Normal' | 'High' | 'Urgent'; status: 'Open' | 'InProgress' | 'WaitingRequester' | 'Resolved' | 'Closed'; consentToShareWorkspaceData: boolean; createdAt: string; updatedAt: string; messages: SupportTicketMessageData[] }

const navItems: NavItem[] = [
  { id: 'hoje', label: 'Hoje', icon: LayoutDashboard },
  { id: 'contas-financeiras', label: 'Contas financeiras', icon: WalletCards },
  { id: 'movimentacoes', label: 'Movimentações', icon: ReceiptText },
  { id: 'caixa', label: 'Fluxo de caixa', icon: BarChart3 },
  { id: 'contas', label: 'Pagar e receber', icon: CalendarDays, badge: 4 },
  { id: 'conciliacao', label: 'Conciliação bancária', icon: Landmark },
  { id: 'estrutura', label: 'Categorias e centros', icon: Tags },
  { id: 'contatos', label: 'Clientes e fornecedores', icon: UsersRound },
  { id: 'acessos', label: 'Equipe e acessos', icon: ShieldCheck },
  { id: 'orcamentos', label: 'Orçamentos', icon: CircleDollarSign },
  { id: 'metas', label: 'Metas', icon: Goal },
  { id: 'relatorios', label: 'Relatórios', icon: FileBarChart },
  { id: 'auditoria', label: 'Auditoria', icon: History },
]
const platformNavItems: NavItem[] = [
  { id: 'administracao', label: 'Administração', icon: ShieldCheck },
  { id: 'desenvolvedor', label: 'Área técnica', icon: Wrench },
]

const demoTransactions: Transaction[] = [
  { name: 'Assinatura Figma', detail: 'Hoje, 09:42 • Nubank PJ', category: 'Software', type: 'Expense', value: -96, occurredOn: '2026-09-18', icon: 'F', tone: 'violet' },
  { name: 'Studio Aurora', detail: 'Hoje, 08:16 • Conta principal', category: 'Receita de serviços', type: 'Income', value: 4850, occurredOn: '2026-09-18', icon: 'A', tone: 'green' },
  { name: 'Google Workspace', detail: 'Ontem, 18:05 • Nubank PJ', category: 'Software', type: 'Expense', value: -81.6, occurredOn: '2026-09-17', icon: 'G', tone: 'blue' },
  { name: 'Coworking Central', detail: 'Ontem, 11:30 • Conta principal', category: 'Escritório', type: 'Expense', value: -690, occurredOn: '2026-09-17', icon: 'C', tone: 'amber' },
  { name: 'Transferência para reserva', detail: '8 set, 16:20 • Reserva', category: 'Transferência', type: 'Expense', value: -1200, occurredOn: '2026-09-08', icon: 'R', tone: 'slate' },
]

const cashBars = [
  { day: '01', income: 28, expense: 18 }, { day: '05', income: 46, expense: 24 },
  { day: '09', income: 31, expense: 38 }, { day: '13', income: 68, expense: 27 },
  { day: '17', income: 42, expense: 45 }, { day: '21', income: 76, expense: 32 },
  { day: '25', income: 54, expense: 41 }, { day: '30', income: 84, expense: 29 },
]

const currency = (value: number) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(Math.abs(value))
const signedCurrency = (value: number) => `${value < 0 ? '− ' : ''}${currency(value)}`
const preferencesStorageKey = 'nexo-preferences'
const defaultPreferences: AppPreferences = { hideValuesByDefault: false, compactMode: false, remindersEnabled: true, reminderDays: 7 }
const readPreferences = (userId?: string): AppPreferences => {
  try {
    const stored = JSON.parse(window.localStorage.getItem(userId ? `${preferencesStorageKey}:${userId}` : preferencesStorageKey) ?? window.localStorage.getItem(preferencesStorageKey) ?? '{}') as Partial<AppPreferences>
    const reminderDays = [1, 3, 7, 15].includes(Number(stored.reminderDays)) ? Number(stored.reminderDays) as ReminderDays : defaultPreferences.reminderDays
    return { ...defaultPreferences, ...stored, reminderDays }
  } catch { return defaultPreferences }
}

const normalizeTransaction = (item: ApiTransaction): Transaction => {
  const date = new Date(`${item.occurredOn}T12:00:00`).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })
  const route = item.counterpartyAccountName ? (item.type === 'Expense' ? `${item.accountName} → ${item.counterpartyAccountName}` : `${item.counterpartyAccountName} → ${item.accountName}`) : item.accountName
  return {
    id: item.id, accountId: item.accountId, name: item.description, detail: `${date} • ${route}`,
    category: item.category, type: item.type as 'Income' | 'Expense', value: item.signedAmount, occurredOn: item.occurredOn, icon: item.description.charAt(0).toUpperCase(),
    tone: item.transferId ? 'slate' : item.type === 'Income' ? 'green' : ['Software', 'Tecnologia'].includes(item.category) ? 'violet' : 'amber',
    transferId: item.transferId, counterpartyAccountId: item.counterpartyAccountId, counterpartyAccountName: item.counterpartyAccountName, isTransfer: Boolean(item.transferId),
  }
}

const buildFinancialInsights = (dashboard: DashboardData | null, commitments: CommitmentData[], budget: BudgetSummaryData | null, goals: GoalsSummaryData | null, preferences: AppPreferences): FinancialInsight[] => {
  const insights: FinancialInsight[] = []
  const actionable = commitments.filter(item => item.status === 'Open' || item.status === 'PartiallySettled')
  const overdue = actionable.filter(item => item.isOverdue)
  if (overdue.length) {
    const total = overdue.reduce((sum, item) => sum + item.remainingAmount, 0)
    insights.push({ id: `overdue:${overdue.map(item => `${item.id}-${item.remainingAmount}`).sort().join('|')}`, title: 'Contas vencidas', message: `${overdue.length} ${overdue.length === 1 ? 'conta está vencida' : 'contas estão vencidas'}, somando ${currency(total)}.`, actionLabel: 'Revisar pendências', target: 'contas', tone: 'critical' })
  }

  const today = new Date(); today.setHours(0, 0, 0, 0)
  const reminderLimit = new Date(today); reminderLimit.setDate(today.getDate() + preferences.reminderDays)
  const upcomingPayables = actionable.filter(item => {
    const due = new Date(`${item.dueDate}T12:00:00`)
    return preferences.remindersEnabled && item.kind === 'Payable' && !item.isOverdue && due >= today && due <= reminderLimit
  })
  if (upcomingPayables.length) {
    const total = upcomingPayables.reduce((sum, item) => sum + item.remainingAmount, 0)
    insights.push({ id: `upcoming-payables:${preferences.reminderDays}:${upcomingPayables.map(item => `${item.id}-${item.remainingAmount}`).sort().join('|')}`, title: 'Pagamentos próximos', message: `${currency(total)} em pagamentos vencem ${preferences.reminderDays === 1 ? 'até amanhã' : `nos próximos ${preferences.reminderDays} dias`}.`, actionLabel: 'Ver pagamentos', target: 'contas', tone: 'attention' })
  }

  const upcomingReceivables = actionable.filter(item => {
    const due = new Date(`${item.dueDate}T12:00:00`)
    return preferences.remindersEnabled && item.kind === 'Receivable' && !item.isOverdue && due >= today && due <= reminderLimit
  })
  if (upcomingReceivables.length) {
    const total = upcomingReceivables.reduce((sum, item) => sum + item.remainingAmount, 0)
    insights.push({ id: `upcoming-receivables:${preferences.reminderDays}:${upcomingReceivables.map(item => `${item.id}-${item.remainingAmount}`).sort().join('|')}`, title: 'Recebimentos previstos', message: `Você tem ${currency(total)} a receber ${preferences.reminderDays === 1 ? 'até amanhã' : `nos próximos ${preferences.reminderDays} dias`}.`, actionLabel: 'Acompanhar recebimentos', target: 'contas', tone: 'positive' })
  }

  const exceededBudgets = budget?.lines.filter(line => line.status === 'Exceeded') ?? []
  const warningBudgets = budget?.lines.filter(line => line.status === 'Warning') ?? []
  if (exceededBudgets.length) insights.push({ id: `budget-exceeded:${exceededBudgets.map(line => `${line.id}-${line.projectedAmount}`).sort().join('|')}`, title: 'Orçamento excedido', message: `${exceededBudgets.length} ${exceededBudgets.length === 1 ? 'orçamento ultrapassou' : 'orçamentos ultrapassaram'} o valor planejado.`, actionLabel: 'Revisar orçamento', target: 'orcamentos', tone: 'critical' })
  else if (warningBudgets.length) insights.push({ id: `budget-warning:${warningBudgets.map(line => `${line.id}-${line.projectedAmount}`).sort().join('|')}`, title: 'Limite de orçamento', message: `${warningBudgets.length} ${warningBudgets.length === 1 ? 'orçamento está' : 'orçamentos estão'} perto do limite mensal.`, actionLabel: 'Acompanhar orçamento', target: 'orcamentos', tone: 'attention' })

  const atRiskGoals = goals?.goals.filter(goal => goal.status === 'AtRisk' || goal.status === 'Overdue') ?? []
  if (atRiskGoals.length) insights.push({ id: `goals-risk:${atRiskGoals.map(goal => `${goal.id}-${goal.currentAmount}`).sort().join('|')}`, title: 'Metas em risco', message: `${atRiskGoals.length} ${atRiskGoals.length === 1 ? 'meta precisa' : 'metas precisam'} de atenção para cumprir o prazo.`, actionLabel: 'Ver metas', target: 'metas', tone: 'attention' })

  if (dashboard && dashboard.monthResult > 0) insights.push({ id: `positive-result:${new Date().getFullYear()}-${new Date().getMonth() + 1}`, title: 'Resultado positivo', message: `Seu resultado no mês está positivo em ${currency(dashboard.monthResult)}.`, actionLabel: 'Ver relatório', target: 'relatorios', tone: 'positive' })
  if (!insights.length) insights.push({ id: 'all-clear', title: 'Tudo em ordem', message: 'Nenhum alerta financeiro importante no momento.', actionLabel: 'Ver visão geral', target: 'hoje', tone: 'positive' })
  const priority = { critical: 0, attention: 1, positive: 2 }
  return insights.sort((a, b) => priority[a.tone] - priority[b.tone])
}

function App() {
  const [authUser, setAuthUser] = useState<AuthUser | null | undefined>(undefined)
  const [active, setActive] = useState('hoje')
  const [preferences, setPreferences] = useState<AppPreferences>(readPreferences)
  const [hidden, setHidden] = useState(() => readPreferences().hideValuesByDefault)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [newEntryOpen, setNewEntryOpen] = useState(false)
  const [toast, setToast] = useState('')
  const [workspaces, setWorkspaces] = useState<WorkspaceData[]>([])
  const [workspaceId, setWorkspaceId] = useState(() => window.localStorage.getItem(workspaceStorageKey) ?? '')
  const [workspaceMenuOpen, setWorkspaceMenuOpen] = useState(false)
  const [workspaceEditing, setWorkspaceEditing] = useState<WorkspaceData | 'new' | null>(null)
  const [query, setQuery] = useState('')
  const [dashboard, setDashboard] = useState<DashboardData | null>(null)
  const [accounts, setAccounts] = useState<Account[]>([])
  const [managedAccounts, setManagedAccounts] = useState<Account[]>([])
  const [liveTransactions, setLiveTransactions] = useState<Transaction[]>(demoTransactions)
  const [apiError, setApiError] = useState(false)
  const [commitments, setCommitments] = useState<CommitmentData[]>([])
  const [categories, setCategories] = useState<CategoryData[]>([])
  const [costCenters, setCostCenters] = useState<CostCenterData[]>([])
  const [budget, setBudget] = useState<BudgetSummaryData | null>(null)
  const [goals, setGoals] = useState<GoalsSummaryData | null>(null)
  const [contacts, setContacts] = useState<ContactData[]>([])
  const [audit, setAudit] = useState<AuditData[]>([])
  const [reconciliationPending, setReconciliationPending] = useState(0)
  const [insightIndex, setInsightIndex] = useState(0)
  const [dismissedInsights, setDismissedInsights] = useState<string[]>([])
  const [readNotifications, setReadNotifications] = useState<string[]>([])
  const [notificationOpen, setNotificationOpen] = useState(false)
  const openCommitments = commitments.filter(item => item.status === 'Open' || item.status === 'PartiallySettled').length
  const allowedPlatformItems = platformNavItems.filter(item => item.id === 'administracao' ? authUser?.globalRole === 'SuperAdmin' || authUser?.globalRole === 'Support' : authUser?.globalRole === 'SuperAdmin' || authUser?.globalRole === 'Developer')
  const activeItem = [...navItems, ...allowedPlatformItems].find((item) => item.id === active) ?? (active === 'configuracoes' ? { id: 'configuracoes', label: 'Configurações', icon: Settings } : active === 'ajuda' ? { id: 'ajuda', label: 'Central de ajuda', icon: HelpCircle } : navItems[0])
  const selectedWorkspace = workspaces.find(workspace => workspace.id === workspaceId) ?? workspaces[0]
  const availableInsights = buildFinancialInsights(dashboard, commitments, budget, goals, preferences).filter(insight => !dismissedInsights.includes(insight.id))
  const currentInsight = availableInsights.length ? availableInsights[insightIndex % availableInsights.length] : null
  const unreadNotifications = availableInsights.filter(insight => insight.id !== 'all-clear' && !readNotifications.includes(insight.id)).length
  const showToast = useCallback((message: string) => { setToast(message); window.setTimeout(() => setToast(''), 2400) }, [])
  const selectNav = (id: string) => { setActive(id); setMobileOpen(false); setNotificationOpen(false) }

  const saveNotificationState = (read: string[], dismissed: string[]) => {
    if (!authUser || !workspaceId) return
    window.localStorage.setItem(`nexo-notifications:${authUser.id}:${workspaceId}`, JSON.stringify({ read, dismissed }))
  }
  const loadNotificationState = useCallback((userId: string, targetWorkspaceId: string) => {
    try {
      const stored = JSON.parse(window.localStorage.getItem(`nexo-notifications:${userId}:${targetWorkspaceId}`) ?? '{}') as { read?: string[]; dismissed?: string[] }
      setReadNotifications(Array.isArray(stored.read) ? stored.read : [])
      setDismissedInsights(Array.isArray(stored.dismissed) ? stored.dismissed : [])
    } catch { setReadNotifications([]); setDismissedInsights([]) }
    setInsightIndex(0)
  }, [])
  const markNotificationRead = (id: string) => {
    const next = readNotifications.includes(id) ? readNotifications : [...readNotifications, id]
    setReadNotifications(next); saveNotificationState(next, dismissedInsights)
  }
  const dismissNotification = (id: string) => {
    const nextDismissed = dismissedInsights.includes(id) ? dismissedInsights : [...dismissedInsights, id]
    const nextRead = readNotifications.includes(id) ? readNotifications : [...readNotifications, id]
    setDismissedInsights(nextDismissed); setReadNotifications(nextRead); setInsightIndex(0); saveNotificationState(nextRead, nextDismissed)
  }
  const markAllNotificationsRead = () => {
    const next = [...new Set([...readNotifications, ...availableInsights.map(insight => insight.id)])]
    setReadNotifications(next); saveNotificationState(next, dismissedInsights)
  }

  const refreshWorkspaces = useCallback(async () => {
    const response = await apiFetch('/api/workspaces')
    if (!response.ok) throw new Error('API unavailable')
    const data = await response.json() as WorkspaceData[]
    setWorkspaces(data)
    const storedId = window.localStorage.getItem(workspaceStorageKey)
    const nextId = data.some(workspace => workspace.id === storedId) ? storedId! : data[0]?.id ?? ''
    if (nextId) window.localStorage.setItem(workspaceStorageKey, nextId)
    setWorkspaceId(nextId)
    if (authUser && nextId) loadNotificationState(authUser.id, nextId)
    return data
  }, [authUser, loadNotificationState])

  const refreshData = useCallback(async () => {
    try {
      const now = new Date()
      const [dashboardResponse, accountsResponse, transactionsResponse, commitmentsResponse, categoriesResponse, centersResponse, budgetResponse, goalsResponse, contactsResponse, auditResponse, reconciliationResponse] = await Promise.all([apiFetch('/api/dashboard'), apiFetch('/api/accounts?includeInactive=true'), apiFetch('/api/transactions'), apiFetch('/api/commitments'), apiFetch('/api/categories'), apiFetch('/api/cost-centers'), apiFetch(`/api/budgets?year=${now.getFullYear()}&month=${now.getMonth() + 1}`), apiFetch('/api/goals'), apiFetch('/api/contacts?includeInactive=true'), apiFetch('/api/audit'), apiFetch('/api/reconciliation')])
      if (!dashboardResponse.ok || !accountsResponse.ok || !transactionsResponse.ok || !commitmentsResponse.ok || !categoriesResponse.ok || !centersResponse.ok || !budgetResponse.ok || !goalsResponse.ok || !contactsResponse.ok || !auditResponse.ok || !reconciliationResponse.ok) throw new Error('API unavailable')
      const dashboardData = await dashboardResponse.json() as DashboardData
      const accountData = await accountsResponse.json() as Account[]
      const transactionData = await transactionsResponse.json() as ApiTransaction[]
      const commitmentData = await commitmentsResponse.json() as CommitmentData[]
      const categoryData = await categoriesResponse.json() as CategoryData[]; const centerData = await centersResponse.json() as CostCenterData[]
      const budgetData = await budgetResponse.json() as BudgetSummaryData
      const goalsData = await goalsResponse.json() as GoalsSummaryData
      const contactData = await contactsResponse.json() as ContactData[]; const auditData = await auditResponse.json() as AuditData[]; const reconciliationData = await reconciliationResponse.json() as ReconciliationData
      setDashboard(dashboardData); setAccounts(dashboardData.accounts); setManagedAccounts(accountData); setLiveTransactions(transactionData.map(normalizeTransaction)); setCommitments(commitmentData); setCategories(categoryData); setCostCenters(centerData); setBudget(budgetData); setGoals(goalsData); setContacts(contactData); setAudit(auditData); setReconciliationPending(reconciliationData.pending); setApiError(false)
    } catch { setApiError(true) }
  }, [])

  useEffect(() => {
    const unauthorized = () => setAuthUser(null)
    window.addEventListener('nexo:unauthorized', unauthorized)
    apiFetch('/api/auth/me').then(async response => {
      const user = response.ok ? await response.json() as AuthUser : null
      if (user) { const userPreferences = readPreferences(user.id); setPreferences(userPreferences); setHidden(userPreferences.hideValuesByDefault) }
      setAuthUser(user)
    }).catch(() => setAuthUser(null))
    return () => window.removeEventListener('nexo:unauthorized', unauthorized)
  }, [])
  useEffect(() => { if (authUser) void (async () => { try { await refreshWorkspaces(); await refreshData() } catch { setApiError(true) } })() }, [authUser, refreshWorkspaces, refreshData])
  const switchWorkspace = async (nextId: string) => {
    if (nextId === workspaceId) { setWorkspaceMenuOpen(false); return }
    window.localStorage.setItem(workspaceStorageKey, nextId); setWorkspaceId(nextId); setWorkspaceMenuOpen(false); setActive('hoje'); if (authUser) loadNotificationState(authUser.id, nextId); setNotificationOpen(false)
    await refreshData(); showToast('Espaço alterado')
  }

  const logout = async () => {
    await apiFetch('/api/auth/logout', { method: 'POST' }).catch(() => null)
    window.localStorage.removeItem(workspaceStorageKey); setWorkspaceId(''); setWorkspaces([]); setAuthUser(null)
  }

  const savePreferences = (next: AppPreferences) => {
    if (authUser) window.localStorage.setItem(`${preferencesStorageKey}:${authUser.id}`, JSON.stringify(next))
    else window.localStorage.setItem(preferencesStorageKey, JSON.stringify(next))
    setPreferences(next)
    if (next.hideValuesByDefault) setHidden(true)
    showToast('Preferências salvas')
  }

  if (authUser === undefined) return <div className="auth-loading"><span className="brand-mark"><TrendingUp size={21} /></span><p>Preparando seu Nexo…</p></div>
  if (authUser === null) return <AuthPage onAuthenticated={user => { const userPreferences = readPreferences(user.id); setPreferences(userPreferences); setHidden(userPreferences.hideValuesByDefault); setAuthUser(user) }} />

  return <div className={`app-shell ${preferences.compactMode ? 'compact-mode' : ''}`}>
    <aside className={`sidebar ${mobileOpen ? 'is-open' : ''}`} aria-label="Navegação principal">
      <div className="brand"><span className="brand-mark"><TrendingUp size={19} strokeWidth={2.5} /></span><span>Nexo</span><button className="mobile-close" onClick={() => setMobileOpen(false)} aria-label="Fechar menu"><X size={20} /></button></div>
      <div className="workspace-control"><button className="workspace-switcher" type="button" onClick={() => setWorkspaceMenuOpen(value => !value)} aria-expanded={workspaceMenuOpen}><span className="workspace-icon"><Building2 size={17} /></span><span><strong>{selectedWorkspace?.name ?? 'Carregando…'}</strong><small>{selectedWorkspace?.kind === 'Personal' ? 'Espaço pessoal' : 'Espaço empresa'}</small></span><ChevronDown size={16} /></button>{workspaceMenuOpen && <div className="workspace-menu"><small>Seus espaços</small>{workspaces.map(workspace => <div className={workspace.id === workspaceId ? 'selected' : ''} key={workspace.id}><button onClick={() => void switchWorkspace(workspace.id)}><span>{workspace.name.charAt(0).toUpperCase()}</span><b>{workspace.name}<small>{workspace.kind === 'Personal' ? 'Pessoal' : 'Empresa'}</small></b>{workspace.id === workspaceId && <CheckCircle2 size={15} />}</button>{workspace.role !== 'Member' && <button className="workspace-edit" onClick={() => { setWorkspaceEditing(workspace); setWorkspaceMenuOpen(false) }} aria-label={`Editar ${workspace.name}`}><Settings size={14} /></button>}</div>)}<button className="workspace-create" onClick={() => { setWorkspaceEditing('new'); setWorkspaceMenuOpen(false) }}><Plus size={15} /> Criar espaço</button></div>}</div>
      <nav className="main-nav"><p className="nav-label">Visão geral</p>{navItems.map((item) => { const Icon = item.icon; const badge = item.id === 'contas' ? openCommitments : item.id === 'conciliacao' ? reconciliationPending : item.badge; return <button key={item.id} className={active === item.id ? 'active' : ''} onClick={() => selectNav(item.id)}><Icon size={18} /><span>{item.label}</span>{badge ? <em>{badge}</em> : null}</button> })}{allowedPlatformItems.length > 0 && <><p className="nav-label platform-nav-label">Plataforma</p>{allowedPlatformItems.map(item => { const Icon = item.icon; return <button key={item.id} className={active === item.id ? 'active' : ''} onClick={() => selectNav(item.id)}><Icon size={18} /><span>{item.label}</span></button> })}</>}</nav>
      <div className="sidebar-bottom">
        {currentInsight && <div className={`insight-card ${currentInsight.tone}`}>
          <div className="insight-card-head"><span><Sparkles size={15} /> Insight</span><button type="button" onClick={() => dismissNotification(currentInsight.id)} aria-label="Dispensar insight" title="Dispensar"><X size={14} /></button></div>
          <p>{currentInsight.message}</p>
          <div className="insight-card-actions"><button type="button" onClick={() => selectNav(currentInsight.target)}>{currentInsight.actionLabel} <ArrowRight size={14} /></button>{availableInsights.length > 1 && <button className="insight-next" type="button" onClick={() => setInsightIndex(index => (index + 1) % availableInsights.length)} aria-label="Próximo insight" title="Próximo insight"><span>{(insightIndex % availableInsights.length) + 1}/{availableInsights.length}</span><ChevronRight size={14} /></button>}</div>
        </div>}
        <button className={`plain-nav ${active === 'ajuda' ? 'active' : ''}`} onClick={() => selectNav('ajuda')}><HelpCircle size={18} /> Central de ajuda</button><button className={`plain-nav ${active === 'configuracoes' ? 'active' : ''}`} onClick={() => selectNav('configuracoes')}><Settings size={18} /> Configurações</button>
        <div className="profile"><span className="avatar">{authUser.name.split(' ').map(part => part[0]).join('').slice(0, 2).toUpperCase()}</span><span><strong>{authUser.name}</strong><small>{authUser.email}</small></span><button onClick={() => void logout()} aria-label="Sair" title="Sair"><LogOut size={17} /></button></div>
      </div>
    </aside>
    {mobileOpen && <button className="mobile-overlay" aria-label="Fechar menu" onClick={() => setMobileOpen(false)} />}
    <main className="main-content">
      <header className="topbar"><button className="menu-button" onClick={() => setMobileOpen(true)} aria-label="Abrir menu"><Menu size={21} /></button><div className="breadcrumbs"><span>{selectedWorkspace?.name ?? 'Espaço'}</span><ChevronRight size={14} /><strong>{activeItem.label}</strong></div><div className="top-actions"><div className="notification-wrap"><button className="icon-button notification-trigger" aria-label={`${unreadNotifications} notificações não lidas`} aria-expanded={notificationOpen} onClick={() => setNotificationOpen(value => !value)}><Bell size={19} />{unreadNotifications > 0 && <em>{unreadNotifications > 9 ? '9+' : unreadNotifications}</em>}</button>{notificationOpen && <><button className="notification-backdrop" aria-label="Fechar notificações" onClick={() => setNotificationOpen(false)} /><section className="notification-popover" aria-label="Central de notificações"><header><div><strong>Notificações</strong><small>{unreadNotifications ? `${unreadNotifications} ${unreadNotifications === 1 ? 'não lida' : 'não lidas'}` : 'Tudo acompanhado'}</small></div>{unreadNotifications > 0 && <button type="button" onClick={markAllNotificationsRead}>Marcar todas como lidas</button>}</header><div className="notification-list">{availableInsights.map(item => <article className={`${item.tone} ${item.id === 'all-clear' || readNotifications.includes(item.id) ? 'read' : 'unread'}`} key={item.id}><button className="notification-main" type="button" onClick={() => { markNotificationRead(item.id); selectNav(item.target) }}><span><Sparkles size={15} /></span><div><strong>{item.title}</strong><p>{item.message}</p><small>{item.actionLabel} <ArrowRight size={12} /></small></div></button><button className="notification-dismiss" type="button" onClick={() => dismissNotification(item.id)} aria-label={`Dispensar ${item.title}`} title="Dispensar"><X size={14} /></button></article>)}{availableInsights.length === 0 && <div className="notification-empty"><CheckCircle2 size={25} /><strong>Nenhuma notificação</strong><span>Você dispensou todos os avisos atuais.</span></div>}</div></section></>}</div><button className="primary-button" onClick={() => setNewEntryOpen(true)}><Plus size={18} /> Novo lançamento</button></div></header>
      {apiError && <div className="api-banner">Dados de demonstração — inicie a API para ativar a persistência.</div>}
      {authUser.globalRole !== 'User' && !authUser.isTwoFactorEnabled && <div className="mfa-required-banner"><ShieldCheck size={17} /><span><strong>Proteção obrigatória</strong> Configure a autenticação em dois fatores para liberar seu acesso privilegiado.</span><button onClick={() => setActive('configuracoes')}>Configurar agora</button></div>}
      {active === 'hoje' && <Dashboard userName={authUser.name} hidden={hidden} setHidden={setHidden} setActive={setActive} setNewEntryOpen={setNewEntryOpen} data={dashboard} transactions={liveTransactions} commitments={commitments} budget={budget} goals={goals} />}
      {active === 'contas-financeiras' && <AccountsPage accounts={managedAccounts} onChanged={refreshData} showToast={showToast} />}
      {active === 'movimentacoes' && <TransactionsPage query={query} setQuery={setQuery} transactions={liveTransactions} accounts={accounts} categories={categories} onChanged={refreshData} showToast={showToast} />}
      {active === 'caixa' && <CashFlowPage />}
      {active === 'contas' && <CommitmentsPage items={commitments} accounts={accounts} contacts={contacts} onChanged={refreshData} showToast={showToast} />}
      {active === 'conciliacao' && <ReconciliationPage accounts={accounts} categories={categories} centers={costCenters} onChanged={refreshData} showToast={showToast} />}
      {active === 'estrutura' && <StructurePage categories={categories} centers={costCenters} accounts={managedAccounts} transactions={liveTransactions} onChanged={refreshData} showToast={showToast} />}
      {active === 'contatos' && <ContactsPage contacts={contacts} onChanged={refreshData} showToast={showToast} />}
      {active === 'acessos' && <WorkspaceAccessPage user={authUser} workspace={selectedWorkspace} onWorkspacesChanged={refreshWorkspaces} showToast={showToast} />}
      {active === 'orcamentos' && <BudgetPage categories={categories} centers={costCenters} onCurrentChanged={refreshData} showToast={showToast} />}
      {active === 'metas' && <GoalsPage data={goals} categories={categories} onChanged={refreshData} showToast={showToast} />}
      {active === 'relatorios' && <ReportsPage centers={costCenters} showToast={showToast} />}
      {active === 'auditoria' && <AuditPage entries={audit} />}
      {active === 'ajuda' && <HelpCenterPage user={authUser} workspace={selectedWorkspace} showToast={showToast} />}
      {active === 'administracao' && (authUser.globalRole === 'SuperAdmin' || authUser.globalRole === 'Support') && <PlatformAdminPage currentUser={authUser} showToast={showToast} />}
      {active === 'desenvolvedor' && (authUser.globalRole === 'SuperAdmin' || authUser.globalRole === 'Developer') && <DeveloperPage showToast={showToast} />}
      {active === 'configuracoes' && <SettingsPage user={authUser} workspace={selectedWorkspace} preferences={preferences} onUserChanged={setAuthUser} onPreferencesChanged={savePreferences} onEditWorkspace={() => selectedWorkspace && setWorkspaceEditing(selectedWorkspace)} showToast={showToast} />}
      {!['hoje', 'contas-financeiras', 'movimentacoes', 'caixa', 'contas', 'conciliacao', 'estrutura', 'contatos', 'acessos', 'orcamentos', 'metas', 'relatorios', 'auditoria', 'ajuda', 'administracao', 'desenvolvedor', 'configuracoes'].includes(active) && <ModulePage item={activeItem} onBack={() => setActive('hoje')} />}
    </main>
    {newEntryOpen && <EntryModal accounts={accounts} categories={categories} onClose={() => setNewEntryOpen(false)} onSaved={async () => { setNewEntryOpen(false); await refreshData(); showToast('Lançamento salvo com sucesso') }} />}
    {workspaceEditing && <WorkspaceModal workspace={workspaceEditing === 'new' ? null : workspaceEditing} onClose={() => setWorkspaceEditing(null)} onSaved={async workspace => { setWorkspaceEditing(null); await refreshWorkspaces(); if (!workspaceEditing || workspaceEditing === 'new') await switchWorkspace(workspace.id); else await refreshData(); showToast(workspaceEditing === 'new' ? 'Espaço criado' : 'Espaço atualizado') }} />}
    {toast && <div className="toast" role="status">{toast}</div>}
  </div>
}

function AuthPage({ onAuthenticated }: { onAuthenticated: (user: AuthUser) => void }) {
  const [linkResetToken] = useState(() => {
    const fragment = new URLSearchParams(window.location.hash.slice(1))
    const token = fragment.get('reset-password') ?? ''
    if (token) window.history.replaceState(null, '', window.location.pathname + window.location.search)
    return token
  })
  const [mode, setMode] = useState<'login' | 'register' | 'forgot' | 'reset' | 'two-factor'>(linkResetToken ? 'reset' : 'login')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [resetToken, setResetToken] = useState(linkResetToken)
  const [challengeToken, setChallengeToken] = useState('')
  const [twoFactorCode, setTwoFactorCode] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [rememberMe, setRememberMe] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [info, setInfo] = useState('')

  const changeMode = (next: typeof mode) => { setMode(next); setError(''); setInfo(''); setPassword('') }
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    try {
      if (mode === 'two-factor') {
        const response = await apiFetch('/api/auth/login/2fa', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ challengeToken, code: twoFactorCode, rememberMe }) })
        const result = await response.json().catch(() => null) as (AuthUser & { detail?: string }) | null
        if (!response.ok || !result) throw new Error(result?.detail ?? 'Código inválido ou expirado.')
        onAuthenticated(result); return
      }
      if (mode === 'forgot') {
        const response = await apiFetch('/api/auth/forgot-password', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email }) })
        const result = await response.json().catch(() => null) as { token?: string; detail?: string } | null
        if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível iniciar a recuperação.')
        setResetToken(result?.token ?? ''); setMode('reset'); setInfo(result?.token ? 'Código temporário gerado. Defina sua nova senha.' : 'Se o e-mail estiver cadastrado, use o código recebido para continuar.'); setSaving(false); return
      }
      if (mode === 'reset') {
        const response = await apiFetch('/api/auth/reset-password', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ token: resetToken, newPassword: password }) })
        if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(problem?.detail ?? 'Não foi possível redefinir a senha.') }
        setMode('login'); setPassword(''); setResetToken(''); setInfo('Senha redefinida. Entre com a nova senha.'); setSaving(false); return
      }
      const response = await apiFetch(`/api/auth/${mode}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(mode === 'login' ? { email, password, rememberMe } : { name, email, password }) })
      const result = await response.json().catch(() => null) as (AuthUser & { requiresTwoFactor?: boolean; challengeToken?: string; detail?: string }) | null
      if (!response.ok || !result) throw new Error(result?.detail ?? 'Não foi possível continuar.')
      if (result.requiresTwoFactor && result.challengeToken) { setChallengeToken(result.challengeToken); setTwoFactorCode(''); setMode('two-factor'); setSaving(false); return }
      onAuthenticated(result)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) }
  }

  const titles = { login: ['BEM-VINDO DE VOLTA', 'Entre na sua conta', 'Acesse seus espaços e continue de onde parou.'], register: ['COMECE AGORA', 'Crie sua conta', 'Organize sua vida financeira em poucos minutos.'], forgot: ['RECUPERAÇÃO', 'Recupere seu acesso', 'Informe o e-mail usado no cadastro.'], reset: ['NOVA SENHA', 'Redefina sua senha', 'Use o código temporário e escolha uma nova senha.'], 'two-factor': ['VERIFICAÇÃO', 'Confirme sua identidade', 'Digite o código do aplicativo autenticador ou um código de recuperação.'] }
  const title = titles[mode]
  return <main className="auth-page"><section className="auth-story"><div className="auth-brand"><span className="brand-mark"><TrendingUp size={19} strokeWidth={2.5} /></span><strong>Nexo</strong></div><div className="auth-message"><p className="eyebrow">CLAREZA PARA DECIDIR</p><h1>Seu financeiro inteiro, conectado em um só lugar.</h1><p>Acompanhe saldos, compromissos, metas e resultados sem perder o contexto do seu negócio.</p><div className="auth-proof"><span><CheckCircle2 size={16} /> Dados separados por espaço</span><span><CheckCircle2 size={16} /> Sessão protegida</span><span><CheckCircle2 size={16} /> Decisões com visão de caixa</span></div></div><small>© 2026 Nexo Financeiro</small></section><section className="auth-form-side"><div className="auth-card"><div className="auth-mobile-brand"><span className="brand-mark"><TrendingUp size={18} /></span><strong>Nexo</strong></div><p className="eyebrow">{title[0]}</p><h2>{title[1]}</h2><p>{title[2]}</p><form onSubmit={submit}>{mode === 'register' && <label>Nome completo<span><UserRound size={17} /><input value={name} onChange={event => setName(event.target.value)} autoComplete="name" placeholder="Como podemos chamar você?" autoFocus /></span></label>}{mode !== 'reset' && mode !== 'two-factor' && <label>E-mail<span><Mail size={17} /><input type="email" value={email} onChange={event => setEmail(event.target.value)} autoComplete="email" placeholder="voce@empresa.com" autoFocus /></span></label>}{mode === 'reset' && <label>Código de recuperação<span><LockKeyhole size={17} /><input value={resetToken} onChange={event => setResetToken(event.target.value)} autoComplete="one-time-code" placeholder="Cole o código temporário" autoFocus /></span></label>}{mode === 'two-factor' && <label>Código de segurança<span><ShieldCheck size={17} /><input value={twoFactorCode} onChange={event => setTwoFactorCode(event.target.value)} autoComplete="one-time-code" placeholder="000000 ou código de recuperação" autoFocus /></span></label>}{mode !== 'forgot' && mode !== 'two-factor' && <label>{mode === 'reset' ? 'Nova senha' : 'Senha'}<span><LockKeyhole size={17} /><input type={showPassword ? 'text' : 'password'} value={password} onChange={event => setPassword(event.target.value)} autoComplete={mode === 'login' ? 'current-password' : 'new-password'} placeholder={mode === 'login' ? 'Sua senha' : 'Mínimo de 8 caracteres'} /><button type="button" onClick={() => setShowPassword(value => !value)} aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}>{showPassword ? <EyeOff size={17} /> : <Eye size={17} />}</button></span></label>}{mode === 'login' && <div className="auth-options"><label><input type="checkbox" checked={rememberMe} onChange={event => setRememberMe(event.target.checked)} /> Manter conectado</label><button type="button" onClick={() => changeMode('forgot')}>Esqueci minha senha</button></div>}{info && <p className="auth-info">{info}</p>}{error && <p className="auth-error">{error}</p>}<button className="auth-submit" disabled={saving}>{saving ? 'Aguarde…' : mode === 'login' ? 'Entrar' : mode === 'register' ? 'Criar conta' : mode === 'forgot' ? 'Gerar código' : mode === 'two-factor' ? 'Verificar código' : 'Redefinir senha'}</button></form><div className="auth-switch"><span>{mode === 'login' ? 'Ainda não tem uma conta?' : mode === 'register' ? 'Já possui uma conta?' : mode === 'two-factor' ? 'Precisa voltar?' : 'Lembrou sua senha?'}</span><button onClick={() => changeMode(mode === 'login' ? 'register' : 'login')}>{mode === 'login' ? 'Criar conta' : 'Entrar'}</button></div></div></section></main>
}

function SettingsPage({ user, workspace, preferences, onUserChanged, onPreferencesChanged, onEditWorkspace, showToast }: { user: AuthUser; workspace?: WorkspaceData; preferences: AppPreferences; onUserChanged: (user: AuthUser) => void; onPreferencesChanged: (preferences: AppPreferences) => void; onEditWorkspace: () => void; showToast: (value: string) => void }) {
  const [name, setName] = useState(user.name)
  const [email, setEmail] = useState(user.email)
  const [profileSaving, setProfileSaving] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [passwordSaving, setPasswordSaving] = useState(false)
  const [localPreferences, setLocalPreferences] = useState(preferences)
  const [error, setError] = useState('')
  const roleLabel = workspace?.role === 'Owner' ? 'Proprietário' : workspace?.role === 'Administrator' ? 'Administrador' : 'Membro'

  const saveProfile = async (event: React.FormEvent) => {
    event.preventDefault(); setProfileSaving(true); setError('')
    try {
      const response = await apiFetch('/api/auth/profile', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, email }) })
      const result = await response.json().catch(() => null) as (AuthUser & { detail?: string }) | null
      if (!response.ok || !result) throw new Error(result?.detail ?? 'Não foi possível atualizar o perfil.')
      onUserChanged(result); setName(result.name); setEmail(result.email); showToast('Perfil atualizado')
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setProfileSaving(false) }
  }

  const changePassword = async (event: React.FormEvent) => {
    event.preventDefault(); setError('')
    if (newPassword !== confirmPassword) { setError('A confirmação da nova senha não confere.'); return }
    setPasswordSaving(true)
    try {
      const response = await apiFetch('/api/auth/password', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ currentPassword, newPassword }) })
      if (!response.ok) { const result = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(result?.detail ?? 'Não foi possível alterar a senha.') }
      setCurrentPassword(''); setNewPassword(''); setConfirmPassword(''); showToast('Senha alterada com segurança')
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setPasswordSaving(false) }
  }

  return <div className="page settings-page">
    <section className="page-heading"><div><p className="eyebrow">SUA CONTA</p><h1>Configurações</h1><p>Gerencie seu perfil, segurança e preferências de visualização.</p></div></section>
    {error && <div className="settings-error" role="alert">{error}<button onClick={() => setError('')} aria-label="Fechar aviso"><X size={15} /></button></div>}
    <div className="settings-grid">
      <section className="settings-card settings-profile"><div className="settings-card-head"><span><UserRound size={19} /></span><div><h2>Perfil</h2><p>Informações usadas para identificar sua conta.</p></div></div><form onSubmit={saveProfile}><label>Nome completo<input value={name} onChange={event => setName(event.target.value)} /></label><label>E-mail<input type="email" value={email} onChange={event => setEmail(event.target.value)} /></label><div className="settings-actions"><button className="primary-button" disabled={profileSaving || !name.trim() || !email.trim()}>{profileSaving ? 'Salvando…' : 'Salvar perfil'}</button></div></form></section>
      <section className="settings-card"><div className="settings-card-head"><span><Building2 size={19} /></span><div><h2>Espaço atual</h2><p>Dados e permissões deste ambiente financeiro.</p></div></div><div className="workspace-settings-summary"><span className="workspace-settings-icon">{workspace?.name.charAt(0).toUpperCase() ?? 'E'}</span><div><strong>{workspace?.name ?? 'Espaço'}</strong><small>{workspace?.kind === 'Personal' ? 'Espaço pessoal' : 'Espaço empresa'} · {roleLabel}</small></div></div>{workspace?.role !== 'Member' && <button className="secondary-button settings-wide-button" onClick={onEditWorkspace}><Settings size={15} /> Editar espaço</button>}</section>
      <section className="settings-card"><div className="settings-card-head"><span><Eye size={19} /></span><div><h2>Visualização</h2><p>Personalize como o Nexo aparece para você.</p></div></div><div className="settings-options"><label className="settings-toggle"><span><strong>Ocultar valores ao entrar</strong><small>Abre saldos e indicadores financeiros protegidos.</small></span><input type="checkbox" checked={localPreferences.hideValuesByDefault} onChange={event => setLocalPreferences(value => ({ ...value, hideValuesByDefault: event.target.checked }))} /><i /></label><label className="settings-toggle"><span><strong>Navegação compacta</strong><small>Reduz o espaçamento dos itens do menu lateral.</small></span><input type="checkbox" checked={localPreferences.compactMode} onChange={event => setLocalPreferences(value => ({ ...value, compactMode: event.target.checked }))} /><i /></label></div><button className="primary-button settings-wide-button" onClick={() => onPreferencesChanged(localPreferences)}>Salvar preferências</button></section>
      <section className="settings-card reminder-settings"><div className="settings-card-head"><span><Bell size={19} /></span><div><h2>Lembretes financeiros</h2><p>Defina quando o Nexo deve avisar sobre vencimentos.</p></div></div><div className="settings-options"><label className="settings-toggle"><span><strong>Avisos de vencimento</strong><small>Mostra pagamentos e recebimentos próximos nos Insights e notificações.</small></span><input type="checkbox" checked={localPreferences.remindersEnabled} onChange={event => setLocalPreferences(value => ({ ...value, remindersEnabled: event.target.checked }))} /><i /></label><label className={`reminder-window ${localPreferences.remindersEnabled ? '' : 'disabled'}`}><span><strong>Avisar com antecedência</strong><small>Contas vencidas continuam sendo exibidas como alertas críticos.</small></span><select value={localPreferences.reminderDays} disabled={!localPreferences.remindersEnabled} onChange={event => setLocalPreferences(value => ({ ...value, reminderDays: Number(event.target.value) as ReminderDays }))}><option value={1}>1 dia antes</option><option value={3}>3 dias antes</option><option value={7}>7 dias antes</option><option value={15}>15 dias antes</option></select></label></div><button className="primary-button settings-wide-button" onClick={() => onPreferencesChanged(localPreferences)}>Salvar lembretes</button></section>
      <section className="settings-card"><div className="settings-card-head"><span><LockKeyhole size={19} /></span><div><h2>Segurança</h2><p>Use ao menos oito caracteres na nova senha.</p></div></div><form onSubmit={changePassword}><label>Senha atual<input type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label><div className="settings-password-row"><label>Nova senha<input type="password" autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label><label>Confirmar senha<input type="password" autoComplete="new-password" value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></label></div><div className="settings-actions"><button className="secondary-button" disabled={passwordSaving || !currentPassword || !newPassword || !confirmPassword}>{passwordSaving ? 'Alterando…' : 'Alterar senha'}</button></div></form></section>
      <TwoFactorSettings user={user} onUserChanged={onUserChanged} showToast={showToast} />
    </div>
  </div>
}

function TwoFactorSettings({ user, onUserChanged, showToast }: { user: AuthUser; onUserChanged: (user: AuthUser) => void; showToast: (value: string) => void }) {
  const [setup, setSetup] = useState<{ secret: string; otpAuthUri: string } | null>(null)
  const [qrCode, setQrCode] = useState('')
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')
  const [recoveryCodes, setRecoveryCodes] = useState<string[]>([])
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const privileged = user.globalRole !== 'User'
  const begin = async () => {
    setSaving(true); setError('')
    try { const response = await apiFetch('/api/auth/2fa/setup', { method: 'POST' }); const result = await response.json() as { secret: string; otpAuthUri: string; detail?: string }; if (!response.ok) throw new Error(result.detail ?? 'Não foi possível iniciar.'); setSetup(result); setQrCode(await QRCode.toDataURL(result.otpAuthUri, { width: 190, margin: 1 })) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.') }
    finally { setSaving(false) }
  }
  const enable = async () => {
    if (!setup) return; setSaving(true); setError('')
    try { const response = await apiFetch('/api/auth/2fa/enable', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ secret: setup.secret, code }) }); const result = await response.json() as { user?: AuthUser; recoveryCodes?: string[]; detail?: string }; if (!response.ok || !result.user) throw new Error(result.detail ?? 'Código inválido.'); onUserChanged(result.user); setRecoveryCodes(result.recoveryCodes ?? []); setSetup(null); setCode(''); showToast('Autenticação em dois fatores ativada') }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.') }
    finally { setSaving(false) }
  }
  const disable = async () => {
    setSaving(true); setError('')
    try { const response = await apiFetch('/api/auth/2fa', { method: 'DELETE', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password, code }) }); const result = await response.json() as (AuthUser & { detail?: string }); if (!response.ok) throw new Error(result.detail ?? 'Não foi possível remover.'); onUserChanged(result); setPassword(''); setCode(''); showToast('Segundo fator removido') }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.') }
    finally { setSaving(false) }
  }
  return <section className="settings-card two-factor-card"><div className="settings-card-head"><span><ShieldCheck size={19} /></span><div><h2>Autenticação em dois fatores</h2><p>Códigos compatíveis com Google Authenticator, Microsoft Authenticator e similares.</p></div></div>{user.isTwoFactorEnabled ? <><div className="two-factor-status"><CheckCircle2 size={17} /><div><strong>Proteção ativada</strong><small>{privileged ? 'Obrigatória para seu acesso privilegiado.' : 'Um código será solicitado após sua senha.'}</small></div></div>{!privileged && <div className="two-factor-disable"><label>Senha atual<input type="password" value={password} onChange={event => setPassword(event.target.value)} /></label><label>Código do autenticador<input value={code} onChange={event => setCode(event.target.value)} inputMode="numeric" /></label><button className="secondary-button" disabled={saving || !password || !code} onClick={() => void disable()}>Remover 2FA</button></div>}</> : setup ? <div className="two-factor-setup"><div className="qr-box">{qrCode && <img src={qrCode} alt="QR Code para configurar autenticador" />}</div><div><p>Escaneie o QR Code ou informe esta chave manualmente:</p><code>{setup.secret}</code><label>Código gerado<input value={code} onChange={event => setCode(event.target.value)} inputMode="numeric" maxLength={6} placeholder="000000" /></label><button className="primary-button" disabled={saving || code.length !== 6} onClick={() => void enable()}>Confirmar e ativar</button></div></div> : <><p className="two-factor-description">{privileged ? 'A ativação é obrigatória para acessar os controles privilegiados da plataforma.' : 'Adicione uma segunda verificação ao seu login.'}</p><button className="primary-button settings-wide-button" disabled={saving} onClick={() => void begin()}>{saving ? 'Preparando…' : 'Configurar autenticador'}</button></>}{recoveryCodes.length > 0 && <div className="recovery-codes"><strong>Salve estes códigos de recuperação agora</strong><p>Cada código funciona uma única vez.</p><div>{recoveryCodes.map(item => <code key={item}>{item}</code>)}</div><button className="secondary-button" onClick={() => { void navigator.clipboard.writeText(recoveryCodes.join('\n')); showToast('Códigos copiados') }}>Copiar códigos</button></div>}{error && <p className="form-error">{error}</p>}</section>
}

function WorkspaceModal({ workspace, onClose, onSaved }: { workspace: WorkspaceData | null; onClose: () => void; onSaved: (workspace: WorkspaceData) => Promise<void> }) {
  const [name, setName] = useState(workspace?.name ?? '')
  const [kind, setKind] = useState<WorkspaceData['kind']>(workspace?.kind ?? 'Business')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    try {
      const response = await apiFetch(workspace ? `/api/workspaces/${workspace.id}` : '/api/workspaces', { method: workspace ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, kind }) })
      const result = await response.json().catch(() => null) as (WorkspaceData & { detail?: string }) | null
      if (!response.ok || !result) throw new Error(result?.detail ?? 'Não foi possível salvar o espaço.')
      await onSaved(result)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) }
  }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal workspace-modal" role="dialog" aria-modal="true" aria-labelledby="workspace-title" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>Organização</p><h2 id="workspace-title">{workspace ? 'Editar espaço' : 'Novo espaço'}</h2></div><button type="button" onClick={onClose} aria-label="Fechar"><X size={20} /></button></div><label>Nome do espaço<input value={name} onChange={event => setName(event.target.value)} placeholder="Ex.: Minha empresa" autoFocus /></label><div className="entry-types workspace-types"><button type="button" className={kind === 'Business' ? 'selected' : ''} onClick={() => setKind('Business')}><Building2 size={18} /> Empresa</button><button type="button" className={kind === 'Personal' ? 'selected' : ''} onClick={() => setKind('Personal')}><WalletCards size={18} /> Pessoal</button></div>{!workspace && <p className="workspace-hint">O novo espaço começa vazio e mantém contas, movimentações e relatórios separados dos demais.</p>}{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Salvando…' : 'Salvar espaço'}</button></div></form></div>
}

function Dashboard({ userName, hidden, setHidden, setActive, setNewEntryOpen, data, transactions, commitments, budget, goals }: { userName: string; hidden: boolean; setHidden: (v: boolean) => void; setActive: (v: string) => void; setNewEntryOpen: (v: boolean) => void; data: DashboardData | null; transactions: Transaction[]; commitments: CommitmentData[]; budget: BudgetSummaryData | null; goals: GoalsSummaryData | null }) {
  const money = (value: number) => hidden ? 'R$ ••••••' : currency(value)
  const totalBalance = data?.totalBalance ?? 48290.42
  const monthIncome = data?.monthIncome ?? 28940
  const monthExpense = data?.monthExpense ?? 15780.6
  const monthResult = data?.monthResult ?? 13159.4
  const displayAccounts = data?.accounts ?? [
    { id: '1', name: 'Conta principal', institution: 'Banco Inter', lastFourDigits: '4581', type: 'Checking', openingBalance: 28739.02, balance: 32640.42, isActive: true, transactionCount: 0 },
    { id: '2', name: 'Reserva', institution: 'Liquidez diária', type: 'Investment', openingBalance: 12000, balance: 12000, isActive: true, transactionCount: 0 },
    { id: '3', name: 'Caixa', institution: 'Dinheiro físico', type: 'Cash', openingBalance: 3650, balance: 3650, isActive: true, transactionCount: 0 },
  ]
  const pending = commitments.filter(item => item.status === 'Open' || item.status === 'PartiallySettled')
  const todayHeading = new Date().toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' }).toUpperCase()
  const budgetUsage = budget?.plannedAmount ? Math.round(budget.projectedAmount / budget.plannedAmount * 100) : 0
  const monthName = new Date(new Date().getFullYear(), new Date().getMonth(), 1).toLocaleDateString('pt-BR', { month: 'long' })
  const firstName = userName.trim().split(/\s+/)[0] || 'Olá'
  return <div className="page dashboard-page">
    <section className="page-heading"><div><p className="eyebrow">{todayHeading}</p><h1>Bom dia, {firstName}</h1><p>Aqui está o pulso financeiro da sua empresa hoje.</p></div><button className="visibility-button" onClick={() => setHidden(!hidden)}>{hidden ? <Eye size={17} /> : <EyeOff size={17} />}{hidden ? 'Mostrar valores' : 'Ocultar valores'}</button></section>
    <section className="balance-hero"><div className="balance-main"><span>Saldo disponível <HelpCircle size={14} /></span><h2>{money(totalBalance)}</h2><p><strong><TrendingUp size={14} /> 8,4%</strong> em relação ao mês passado</p></div><div className="balance-divider" />{displayAccounts.slice(0, 3).map(account => <div className="account-summary" key={account.id}><span>{account.name}</span><strong>{money(account.balance)}</strong><small>{account.institution}{account.lastFourDigits ? ` • ${account.lastFourDigits}` : ''}</small></div>)}<button className="hero-action" onClick={() => setActive('contas-financeiras')} aria-label="Gerenciar contas"><ArrowRight size={18} /></button></section>
    <section className="metric-grid">
      <Metric icon={<ArrowDownLeft size={18} />} tone="positive" label="Receitas no mês" value={money(monthIncome)} detail="+12,6% vs. mês anterior" />
      <Metric icon={<ArrowUpRight size={18} />} tone="negative" label="Despesas no mês" value={money(monthExpense)} detail="+4,2% vs. mês anterior" warning />
      <Metric icon={<CalendarDays size={18} />} tone="neutral" label="Resultado do mês" value={money(monthResult)} detail={`${monthIncome ? ((monthResult / monthIncome) * 100).toFixed(1).replace('.', ',') : '0'}% de margem`} />
      <article className="metric-card alert-card"><div><span className="metric-icon alert"><Bell size={18} /></span><small>Precisam de atenção</small></div><strong>{pending.length} pendências</strong><button onClick={() => setActive('contas')}>Revisar agora <ArrowRight size={14} /></button></article>
    </section>
    <section className="dashboard-grid">
      <article className="panel cash-panel"><PanelHead title="Fluxo de caixa" subtitle="Entradas e saídas no mês" action={<button className="period-button">Este mês <ChevronDown size={14} /></button>} /><div className="chart-summary"><span><i className="dot income" /> Entradas <strong>{money(monthIncome)}</strong></span><span><i className="dot expense" /> Saídas <strong>{money(monthExpense)}</strong></span></div><div className="bar-chart" aria-label="Gráfico de entradas e saídas no mês"><div className="grid-lines"><i /><i /><i /><i /></div>{cashBars.map((bar) => <div className="bar-group" key={bar.day}><div className="bars"><i className="income-bar" style={{ height: `${bar.income}%` }} /><i className="expense-bar" style={{ height: `${bar.expense}%` }} /></div><span>{bar.day}</span></div>)}</div><div className="projection-note"><Sparkles size={16} /><span><strong>Projeção positiva:</strong> mantendo este ritmo, você fecha o mês com {money(totalBalance + monthResult)}.</span><button onClick={() => setActive('caixa')}>Ver detalhes</button></div></article>
      <article className="panel attention-panel"><PanelHead title="Próximos compromissos" subtitle="Pendências prioritárias" action={<button className="icon-button"><MoreHorizontal size={19} /></button>} />{pending.slice(0, 3).map(item => <UpcomingCommitment item={item} money={money} key={item.id} />)}{pending.length === 0 && <div className="mini-empty">Nenhuma pendência financeira.</div>}<button className="panel-link" onClick={() => setActive('contas')}>Ver todos os compromissos <ArrowRight size={15} /></button></article>
      <article className="panel transactions-panel"><PanelHead title="Últimas movimentações" subtitle="Atualizado agora" action={<button className="panel-link inline" onClick={() => setActive('movimentacoes')}>Ver todas <ArrowRight size={14} /></button>} /><div className="transaction-list">{transactions.slice(0, 4).map((item) => <TransactionRow key={item.id ?? item.name} item={item} hidden={hidden} />)}</div></article>
      <article className="panel budget-panel"><PanelHead title="Orçamento mensal" subtitle={monthName} action={<button className="icon-button"><MoreHorizontal size={19} /></button>} /><div className="budget-total"><span>Projetado</span><strong>{hidden ? '••%' : `${budgetUsage}%`}</strong></div><div className="progress"><i style={{ width: `${Math.min(budgetUsage, 100)}%` }} /></div><div className="budget-values"><span>{money(budget?.projectedAmount ?? 0)} projetados</span><span>de {money(budget?.plannedAmount ?? 0)}</span></div>{budget?.lines.slice(0, 2).map((line, index) => <BudgetCategory tone={index ? 'amber' : 'violet'} label={line.targetName} used={money(line.projectedAmount)} total={money(line.plannedAmount)} key={line.id} />)}{!budget?.lines.length && <div className="mini-empty">Defina limites para acompanhar os gastos.</div>}<button className="panel-link" onClick={() => setActive('orcamentos')}>Acompanhar orçamento <ArrowRight size={15} /></button></article>
      <article className="panel goal-summary-panel"><PanelHead title="Metas financeiras" subtitle={`${goals?.activeCount ?? 0} metas ativas`} action={<button className="panel-link inline" onClick={() => setActive('metas')}>Ver metas <ArrowRight size={14} /></button>} /><div className="dashboard-goals">{goals?.goals.filter(item => item.isActive).slice(0, 3).map(item => <div key={item.id}><span><strong>{item.name}</strong><small>{Math.min(item.progressPercentage, 100).toLocaleString('pt-BR')}%</small></span><div><i className={item.status.toLowerCase()} style={{ width: `${Math.min(item.progressPercentage, 100)}%` }} /></div><small>{money(item.currentAmount)} de {money(item.targetAmount)}</small></div>)}{!goals?.activeCount && <div className="mini-empty">Crie metas para acompanhar prioridades financeiras.</div>}</div></article>
    </section><button className="floating-add" onClick={() => setNewEntryOpen(true)} aria-label="Novo lançamento"><Plus size={22} /></button>
  </div>
}

const accountTypeLabel: Record<Account['type'], string> = { Checking: 'Conta corrente', Savings: 'Poupança', Cash: 'Dinheiro', Investment: 'Investimento' }

function AccountsPage({ accounts, onChanged, showToast }: { accounts: Account[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [editing, setEditing] = useState<Account | 'new' | null>(null)
  const [showInactive, setShowInactive] = useState(false)
  const [busyId, setBusyId] = useState('')
  const activeAccounts = accounts.filter(account => account.isActive)
  const visible = showInactive ? accounts : activeAccounts
  const totalBalance = activeAccounts.reduce((sum, account) => sum + account.balance, 0)
  const totalOpening = activeAccounts.reduce((sum, account) => sum + account.openingBalance, 0)

  const toggle = async (account: Account) => {
    setBusyId(account.id)
    try {
      const response = await apiFetch(`/api/accounts/${account.id}/active`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ active: !account.isActive }) })
      const problem = await response.json().catch(() => null) as { detail?: string } | null
      if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível alterar a conta.')
      await onChanged()
      showToast(account.isActive ? 'Conta arquivada' : 'Conta reativada')
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setBusyId('') }
  }

  return <div className="page accounts-page">
    <section className="page-heading accounts-heading"><div><p className="eyebrow">ESTRUTURA FINANCEIRA</p><h1>Contas financeiras</h1><p>Centralize saldos bancários, reservas, investimentos e dinheiro em caixa.</p></div><button className="primary-button" onClick={() => setEditing('new')}><Plus size={17} /> Nova conta</button></section>
    <section className="account-kpis"><article><small>Saldo consolidado</small><strong>{signedCurrency(totalBalance)}</strong><span>Somente contas ativas</span></article><article><small>Saldo inicial</small><strong>{signedCurrency(totalOpening)}</strong><span>Base de todas as contas</span></article><article><small>Contas ativas</small><strong>{activeAccounts.length}</strong><span>de {accounts.length} cadastradas</span></article></section>
    <section className="account-panel">
      <div className="account-panel-head"><div><h3>Suas contas</h3><p>Os saldos incluem o valor inicial e todas as movimentações.</p></div><label className="inactive-toggle"><input type="checkbox" checked={showInactive} onChange={event => setShowInactive(event.target.checked)} /> Mostrar arquivadas</label></div>
      <div className="account-grid">{visible.map(account => <article className={`account-card ${account.isActive ? '' : 'inactive'}`} key={account.id}>
        <div className="account-card-head"><span className={`account-type-icon ${account.type.toLowerCase()}`}>{account.type === 'Cash' ? <CircleDollarSign size={20} /> : account.type === 'Investment' ? <TrendingUp size={20} /> : <Landmark size={20} />}</span><span className={`account-state ${account.isActive ? 'active' : ''}`}>{account.isActive ? 'Ativa' : 'Arquivada'}</span></div>
        <div className="account-card-name"><h3>{account.name}</h3><p>{account.institution}{account.lastFourDigits ? ` • final ${account.lastFourDigits}` : ''}</p></div>
        <strong className={`account-balance ${account.balance < 0 ? 'negative-text' : ''}`}>{signedCurrency(account.balance)}</strong><span className="account-kind">{accountTypeLabel[account.type]} · {account.transactionCount} {account.transactionCount === 1 ? 'movimentação' : 'movimentações'}</span>
        <div className="account-card-actions"><button className="secondary-button" onClick={() => setEditing(account)}>Editar</button><button className={account.isActive ? 'archive-account' : 'restore-account'} onClick={() => void toggle(account)} disabled={busyId === account.id}>{account.isActive ? <><Ban size={14} /> Arquivar</> : <><RotateCcw size={14} /> Reativar</>}</button></div>
      </article>)}</div>
      {!visible.length && <div className="account-empty"><WalletCards size={25} /><strong>Nenhuma conta para exibir</strong><span>Cadastre uma conta para começar a registrar movimentações.</span></div>}
    </section>
    {editing && <AccountModal account={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={async isNew => { setEditing(null); await onChanged(); showToast(isNew ? 'Conta criada' : 'Conta atualizada') }} />}
  </div>
}

function AccountModal({ account, onClose, onSaved }: { account: Account | null; onClose: () => void; onSaved: (isNew: boolean) => Promise<void> }) {
  const [name, setName] = useState(account?.name ?? '')
  const [institution, setInstitution] = useState(account?.institution ?? '')
  const [lastFourDigits, setLastFourDigits] = useState(account?.lastFourDigits ?? '')
  const [type, setType] = useState<Account['type']>(account?.type ?? 'Checking')
  const [openingBalance, setOpeningBalance] = useState(account ? account.openingBalance.toFixed(2).replace('.', ',') : '0,00')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    const parsedBalance = Number(openingBalance.replace(/\./g, '').replace(',', '.'))
    try {
      if (!Number.isFinite(parsedBalance)) throw new Error('Informe um saldo inicial válido.')
      const response = await apiFetch(account ? `/api/accounts/${account.id}` : '/api/accounts', { method: account ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, institution, lastFourDigits: lastFourDigits || null, type, openingBalance: parsedBalance }) })
      const problem = await response.json().catch(() => null) as { detail?: string } | null
      if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível salvar a conta.')
      await onSaved(!account)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) }
  }

  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal account-modal" role="dialog" aria-modal="true" aria-labelledby="account-modal-title" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>Conta financeira</p><h2 id="account-modal-title">{account ? 'Editar conta' : 'Nova conta'}</h2></div><button type="button" onClick={onClose} aria-label="Fechar"><X size={20} /></button></div>
    <label>Nome da conta<input value={name} onChange={event => setName(event.target.value)} placeholder="Ex.: Conta principal" autoFocus /></label>
    <div className="field-row"><label>Instituição<input value={institution} onChange={event => setInstitution(event.target.value)} placeholder="Ex.: Banco Inter" /></label><label>Tipo<select value={type} onChange={event => setType(event.target.value as Account['type'])}><option value="Checking">Conta corrente</option><option value="Savings">Poupança</option><option value="Cash">Dinheiro</option><option value="Investment">Investimento</option></select></label></div>
    <div className="field-row"><label>Saldo inicial<input value={openingBalance} onChange={event => setOpeningBalance(event.target.value)} inputMode="decimal" placeholder="R$ 0,00" /></label><label>Últimos 4 dígitos<input value={lastFourDigits} onChange={event => setLastFourDigits(event.target.value.replace(/\D/g, '').slice(0, 4))} inputMode="numeric" maxLength={4} placeholder="Opcional" /></label></div>
    {account?.transactionCount ? <p className="account-edit-note">Alterar o saldo inicial recalcula o saldo atual; as {account.transactionCount} movimentações permanecem intactas.</p> : null}
    {error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Salvando…' : 'Salvar conta'}</button></div>
  </form></div>
}

function EntryModal({ accounts, categories, transaction, onClose, onSaved }: { accounts: Account[]; categories: CategoryData[]; transaction?: Transaction | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const transferSource = transaction?.isTransfer && transaction.type === 'Income' ? transaction.counterpartyAccountId : transaction?.accountId
  const transferDestination = transaction?.isTransfer && transaction.type === 'Income' ? transaction.accountId : transaction?.counterpartyAccountId
  const [type, setType] = useState<'Income' | 'Expense' | 'Transfer'>(transaction?.isTransfer ? 'Transfer' : transaction?.type ?? 'Income')
  const [description, setDescription] = useState(transaction?.name ?? '')
  const [amount, setAmount] = useState(transaction ? Math.abs(transaction.value).toFixed(2).replace('.', ',') : '')
  const [category, setCategory] = useState(transaction?.category ?? 'Receita de serviços')
  const [accountId, setAccountId] = useState(transferSource ?? accounts[0]?.id ?? '')
  const [destinationAccountId, setDestinationAccountId] = useState(transferDestination ?? accounts.find(account => account.id !== accounts[0]?.id)?.id ?? '')
  const [date, setDate] = useState(transaction?.occurredOn ?? financialDate())
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  const changeType = (nextType: 'Income' | 'Expense' | 'Transfer') => {
    setType(nextType)
    if (nextType !== 'Transfer') setCategory(categories.find(item => item.isActive && (item.kind === nextType || item.kind === 'Both'))?.name ?? (nextType === 'Income' ? 'Receita de serviços' : 'Operacional'))
  }

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    const parsedAmount = Number(amount.replace(/\./g, '').replace(',', '.'))
    if (!accountId) { setError('Inicie a API para carregar as contas.'); return }
    if (type === 'Transfer' && (!destinationAccountId || destinationAccountId === accountId)) { setError('Escolha contas de origem e destino diferentes.'); return }
    if (!description.trim() || !Number.isFinite(parsedAmount) || parsedAmount <= 0) { setError('Preencha uma descrição e um valor válido.'); return }
    setSaving(true); setError('')
    try {
      const isTransfer = type === 'Transfer'
      const endpoint = transaction ? (isTransfer ? `/api/transfers/${transaction.transferId}` : `/api/transactions/${transaction.id}`) : (isTransfer ? '/api/transfers' : '/api/transactions')
      const response = await apiFetch(endpoint, { method: transaction ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(isTransfer ? { sourceAccountId: accountId, destinationAccountId, description, amount: parsedAmount, occurredOn: date } : { accountId, description, category, type, amount: parsedAmount, occurredOn: date }) })
      if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(problem?.detail ?? 'Não foi possível salvar o lançamento.') }
      await onSaved()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Não foi possível conectar à API.'); setSaving(false) }
  }

  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal" role="dialog" aria-modal="true" aria-labelledby="entry-title" onSubmit={submit} onMouseDown={(event) => event.stopPropagation()}>
    <div className="modal-head"><div><p>{transaction ? 'Edição' : 'Novo'}</p><h2 id="entry-title">{transaction ? transaction.isTransfer ? 'Editar transferência' : 'Editar lançamento' : 'Registrar lançamento'}</h2></div><button type="button" onClick={onClose} aria-label="Fechar"><X size={20} /></button></div>
    <div className="entry-types"><button type="button" disabled={Boolean(transaction?.isTransfer)} className={type === 'Income' ? 'selected' : ''} onClick={() => changeType('Income')}><ArrowDownLeft size={18} /> Receita</button><button type="button" disabled={Boolean(transaction?.isTransfer)} className={type === 'Expense' ? 'selected' : ''} onClick={() => changeType('Expense')}><ArrowUpRight size={18} /> Despesa</button><button type="button" disabled={Boolean(transaction && !transaction.isTransfer)} className={type === 'Transfer' ? 'selected' : ''} onClick={() => changeType('Transfer')}><ArrowLeftRight size={18} /> Transferência</button></div>
    <label>Descrição<input value={description} onChange={(event) => setDescription(event.target.value)} placeholder="Ex.: Projeto website" autoFocus /></label>
    <div className="field-row"><label>Valor<input value={amount} onChange={(event) => setAmount(event.target.value)} inputMode="decimal" placeholder="R$ 0,00" /></label><label>Data<input type="date" value={date} onChange={(event) => setDate(event.target.value)} /></label></div>
    {type !== 'Transfer' && <label>Categoria{categories.length ? <select value={category} onChange={(event) => setCategory(event.target.value)}>{categories.filter(item => item.isActive && (item.kind === type || item.kind === 'Both')).map(item => <option value={item.name} key={item.id}>{item.parentName ? `${item.parentName} › ` : ''}{item.name}</option>)}</select> : <input value={category} onChange={(event) => setCategory(event.target.value)} />}</label>}
    <label>{type === 'Transfer' ? 'Conta de origem' : 'Conta'}<select value={accountId} onChange={(event) => { setAccountId(event.target.value); if (event.target.value === destinationAccountId) setDestinationAccountId(accounts.find(account => account.id !== event.target.value)?.id ?? '') }}><option value="" disabled>Selecione uma conta</option>{accounts.map(account => <option value={account.id} key={account.id}>{account.name}{account.lastFourDigits ? ` • ${account.lastFourDigits}` : ''}</option>)}</select></label>
    {type === 'Transfer' && <label>Conta de destino<select value={destinationAccountId} onChange={event => setDestinationAccountId(event.target.value)}><option value="" disabled>Selecione uma conta</option>{accounts.filter(account => account.id !== accountId).map(account => <option value={account.id} key={account.id}>{account.name}{account.lastFourDigits ? ` • ${account.lastFourDigits}` : ''}</option>)}</select></label>}
    {error && <p className="form-error">{error}</p>}
    <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Salvando…' : 'Salvar lançamento'}</button></div>
  </form></div>
}

function Metric({ icon, tone, label, value, detail, warning = false }: { icon: React.ReactNode; tone: string; label: string; value: string; detail: string; warning?: boolean }) { return <article className="metric-card"><div><span className={`metric-icon ${tone}`}>{icon}</span><small>{label}</small></div><strong>{value}</strong><p><b className={warning ? 'warning' : ''}>{detail.split(' ')[0]}</b> {detail.split(' ').slice(1).join(' ')}</p></article> }
function PanelHead({ title, subtitle, action }: { title: string; subtitle: string; action: React.ReactNode }) { return <div className="panel-head"><div><h3>{title}</h3><p>{subtitle}</p></div>{action}</div> }
function UpcomingCommitment({ item, money }: { item: CommitmentData; money: (value: number) => string }) {
  const date = new Date(`${item.dueDate}T12:00:00`)
  return <div className={`commitment ${item.isOverdue ? 'overdue' : item.kind === 'Receivable' ? 'receive' : ''}`}><span className="date-box"><b>{date.getDate().toString().padStart(2, '0')}</b><small>{date.toLocaleDateString('pt-BR', { month: 'short' }).replace('.', '').toUpperCase()}</small></span><div><strong>{item.description}</strong><small>{item.isOverdue ? 'Vencido' : item.contact}</small></div><b>{item.kind === 'Receivable' ? '+ ' : ''}{money(item.remainingAmount)}</b></div>
}
function BudgetCategory({ tone, label, used, total }: { tone: string; label: string; used: string; total: string }) { return <div className="budget-category"><span><i className={`category-dot ${tone}`} />{label}</span><strong>{used} <small>/ {total}</small></strong></div> }
function TransactionRow({ item, hidden = false, onEdit, onDelete, onAttachments, attachmentCount = 0, deleting = false }: { item: Transaction; hidden?: boolean; onEdit?: () => void; onDelete?: () => void; onAttachments?: () => void; attachmentCount?: number; deleting?: boolean }) { return <div className="transaction-row"><span className={`transaction-logo ${item.tone}`}>{item.icon}</span><div className="transaction-name"><strong>{item.name}</strong><small>{item.detail}</small></div><span className="category-pill">{item.category}</span><strong className={item.value > 0 ? 'value-positive' : ''}>{hidden ? 'R$ ••••' : `${item.value > 0 ? '+' : '−'} ${currency(item.value)}`}</strong>{onEdit || onDelete || onAttachments ? <span className="row-actions">{onAttachments && <button className={`row-menu attachment-button ${attachmentCount ? 'has-files' : ''}`} onClick={onAttachments} aria-label={`Anexos de ${item.name}`} title="Anexos"><Paperclip size={15} />{attachmentCount > 0 && <em>{attachmentCount}</em>}</button>}{onEdit && <button className="row-menu" onClick={onEdit} aria-label={`Editar ${item.name}`} title="Editar"><Pencil size={15} /></button>}{onDelete && <button className="row-menu delete-transaction" onClick={onDelete} disabled={deleting} aria-label={`Estornar ${item.name}`} title="Estornar lançamento"><RotateCcw size={16} /></button>}</span> : <button className="row-menu" aria-label={`Mais opções para ${item.name}`}><MoreHorizontal size={18} /></button>}</div> }

function TransactionsPage({ query, setQuery, transactions, accounts, categories: categoryOptions, onChanged, showToast }: { query: string; setQuery: (v: string) => void; transactions: Transaction[]; accounts: Account[]; categories: CategoryData[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [period, setPeriod] = useState(financialDate().slice(0, 7))
  const [type, setType] = useState<'all' | 'Income' | 'Expense' | 'Transfer'>('all')
  const [category, setCategory] = useState('all')
  const [deleteTarget, setDeleteTarget] = useState<Transaction | null>(null)
  const [editing, setEditing] = useState<Transaction | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [attachmentTarget, setAttachmentTarget] = useState<Transaction | null>(null)
  const [attachmentCounts, setAttachmentCounts] = useState<Record<string, number>>({})
  const loadAttachmentCounts = async () => { const response = await apiFetch('/api/attachments/counts?entityType=Transaction'); if (response.ok) setAttachmentCounts(await response.json() as Record<string, number>) }
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void loadAttachmentCounts() }, [transactions])
  const categories = [...new Set(transactions.map(item => item.category))].sort((a, b) => a.localeCompare(b, 'pt-BR'))
  const visible = transactions.filter(item => {
    const searchMatches = `${item.name} ${item.category} ${item.detail}`.toLowerCase().includes(query.trim().toLowerCase())
    const typeMatches = type === 'all' || (type === 'Transfer' ? item.isTransfer : item.type === type && !item.isTransfer)
    return searchMatches && (!period || item.occurredOn.startsWith(period)) && typeMatches && (category === 'all' || item.category === category)
  })
  const income = visible.filter(item => item.type === 'Income' && !item.isTransfer).reduce((sum, item) => sum + Math.abs(item.value), 0)
  const expense = visible.filter(item => item.type === 'Expense' && !item.isTransfer).reduce((sum, item) => sum + Math.abs(item.value), 0)
  const periodLabel = period ? new Date(`${period}-01T12:00:00`).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }) : 'todos os períodos'

  const remove = async () => {
    if (!deleteTarget?.id) return
    setDeleting(true)
    try {
      const response = await apiFetch(`/api/transactions/${deleteTarget.id}`, { method: 'DELETE' })
      if (!response.ok) throw new Error()
      setDeleteTarget(null)
      await onChanged()
      showToast(deleteTarget.isTransfer ? 'Transferência estornada' : 'Lançamento estornado')
    } catch { showToast('Não foi possível estornar o lançamento.') }
    finally { setDeleting(false) }
  }

  return <div className="page module-list-page">
    <section className="page-heading"><div><p className="eyebrow">OPERAÇÃO</p><h1>Movimentações</h1><p>Encontre, revise e organize tudo que entrou ou saiu.</p></div></section>
    <section className="list-panel">
      <div className="list-toolbar transaction-filters">
        <label className="search-box"><Search size={18} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar por descrição, conta ou categoria" /></label>
        <label className="filter-field"><SlidersHorizontal size={15} /><select value={type} onChange={event => setType(event.target.value as typeof type)} aria-label="Filtrar por tipo"><option value="all">Todos os tipos</option><option value="Income">Receitas</option><option value="Expense">Despesas</option><option value="Transfer">Transferências</option></select></label>
        <label className="filter-field"><Tags size={15} /><select value={category} onChange={event => setCategory(event.target.value)} aria-label="Filtrar por categoria"><option value="all">Todas as categorias</option>{categories.map(item => <option value={item} key={item}>{item}</option>)}</select></label>
        <label className="filter-field period-filter"><CalendarDays size={15} /><input type="month" value={period} onChange={event => setPeriod(event.target.value)} aria-label="Filtrar por mês" /></label>
        {(query || type !== 'all' || category !== 'all' || !period) && <button className="clear-filters" onClick={() => { setQuery(''); setType('all'); setCategory('all'); setPeriod(financialDate().slice(0, 7)) }}>Limpar</button>}
      </div>
      <div className="list-summary"><div><small>Resultado em {periodLabel}</small><strong className={income - expense < 0 ? 'negative-text' : ''}>{income - expense < 0 ? '− ' : ''}{currency(income - expense)}</strong></div><span><TrendingUp size={15} /> {currency(income)} em receitas</span><span><TrendingDown size={15} /> {currency(expense)} em despesas</span><em>{visible.length} {visible.length === 1 ? 'lançamento' : 'lançamentos'}</em></div>
      <div className="table-head"><span>Descrição</span><span>Categoria</span><span>Valor</span><span /></div>
      <div className="transaction-list full">{visible.length ? visible.map(item => <TransactionRow key={item.id ?? `${item.name}-${item.occurredOn}`} item={item} onEdit={item.id ? () => setEditing(item) : undefined} onDelete={item.id ? () => setDeleteTarget(item) : undefined} onAttachments={item.id ? () => setAttachmentTarget(item) : undefined} attachmentCount={item.id ? attachmentCounts[item.id] ?? 0 : 0} deleting={deleting} />) : <div className="empty-search"><Search size={24} /><strong>Nenhuma movimentação encontrada</strong><span>Ajuste a busca ou os filtros para ver outros lançamentos.</span></div>}</div>
    </section>
    {editing && <EntryModal accounts={accounts} categories={categoryOptions} transaction={editing} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await onChanged(); showToast(editing.isTransfer ? 'Transferência atualizada' : 'Lançamento atualizado') }} />}
    {deleteTarget && <div className="modal-backdrop" onMouseDown={() => !deleting && setDeleteTarget(null)}><div className="entry-modal delete-modal" role="dialog" aria-modal="true" aria-labelledby="delete-title" onMouseDown={event => event.stopPropagation()}><div className="delete-icon"><RotateCcw size={21} /></div><h2 id="delete-title">Estornar {deleteTarget.isTransfer ? 'transferência' : 'lançamento'}?</h2><p><strong>{deleteTarget.name}</strong>, no valor de {currency(deleteTarget.value)}, deixará de compor os saldos, mas será preservado na auditoria.{deleteTarget.isTransfer ? ' As duas movimentações vinculadas serão estornadas.' : ''}</p><div className="modal-actions"><button className="secondary-button" onClick={() => setDeleteTarget(null)} disabled={deleting}>Cancelar</button><button className="danger-button" onClick={remove} disabled={deleting}>{deleting ? 'Estornando…' : 'Estornar'}</button></div></div></div>}
    {attachmentTarget?.id && <AttachmentModal entityType="Transaction" entityId={attachmentTarget.id} title={attachmentTarget.name} onClose={() => setAttachmentTarget(null)} onChanged={loadAttachmentCounts} showToast={showToast} />}
  </div>
}

function CommitmentsPage({ items, accounts, contacts, onChanged, showToast }: { items: CommitmentData[]; accounts: Account[]; contacts: ContactData[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [filter, setFilter] = useState<'all' | 'Receivable' | 'Payable'>('all')
  const [statusFilter, setStatusFilter] = useState<'all' | 'open' | 'overdue' | 'settled' | 'cancelled'>('all')
  const [createOpen, setCreateOpen] = useState(false)
  const [editing, setEditing] = useState<CommitmentData | null>(null)
  const [settling, setSettling] = useState<CommitmentData | null>(null)
  const [attachmentTarget, setAttachmentTarget] = useState<CommitmentData | null>(null)
  const [attachmentCounts, setAttachmentCounts] = useState<Record<string, number>>({})
  const loadAttachmentCounts = async () => { const response = await apiFetch('/api/attachments/counts?entityType=Commitment'); if (response.ok) setAttachmentCounts(await response.json() as Record<string, number>) }
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void loadAttachmentCounts() }, [items])
  const openItems = items.filter(item => item.status === 'Open' || item.status === 'PartiallySettled')
  const visible = items.filter(item => {
    const kindMatches = filter === 'all' || item.kind === filter
    const statusMatches = statusFilter === 'all'
      || (statusFilter === 'open' && (item.status === 'Open' || item.status === 'PartiallySettled') && !item.isOverdue)
      || (statusFilter === 'overdue' && item.isOverdue)
      || (statusFilter === 'settled' && item.status === 'Settled')
      || (statusFilter === 'cancelled' && item.status === 'Cancelled')
    return kindMatches && statusMatches
  })
  const receivable = openItems.filter(item => item.kind === 'Receivable').reduce((sum, item) => sum + item.remainingAmount, 0)
  const payable = openItems.filter(item => item.kind === 'Payable').reduce((sum, item) => sum + item.remainingAmount, 0)
  const overdue = openItems.filter(item => item.isOverdue).reduce((sum, item) => sum + item.remainingAmount, 0)

  const cancel = async (item: CommitmentData) => {
    if (!window.confirm(`Cancelar “${item.description}”?`)) return
    const response = await apiFetch(`/api/commitments/${item.id}/cancel`, { method: 'POST' })
    if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; showToast(problem?.detail ?? 'Não foi possível cancelar.'); return }
    await onChanged(); showToast('Compromisso cancelado')
  }

  return <div className="page commitments-page">
    <section className="page-heading"><div><p className="eyebrow">AGENDA FINANCEIRA</p><h1>Pagar e receber</h1><p>Antecipe compromissos e mantenha seu caixa sob controle.</p></div><button className="primary-button" onClick={() => setCreateOpen(true)}><Plus size={17} /> Novo compromisso</button></section>
    <section className="commitment-kpis"><article><span className="kpi-symbol receivable"><ArrowDownLeft size={18} /></span><div><small>A receber</small><strong>{currency(receivable)}</strong></div></article><article><span className="kpi-symbol payable"><ArrowUpRight size={18} /></span><div><small>A pagar</small><strong>{currency(payable)}</strong></div></article><article><span className="kpi-symbol overdue"><Clock3 size={18} /></span><div><small>Em atraso</small><strong>{currency(overdue)}</strong></div></article><article><span className="kpi-symbol balance"><WalletCards size={18} /></span><div><small>Saldo previsto</small><strong>{currency(receivable - payable)}</strong></div></article></section>
    <section className="commitments-panel"><div className="commitment-toolbar"><div className="commitment-tabs">{([['all', 'Todos'], ['Receivable', 'A receber'], ['Payable', 'A pagar']] as const).map(([id, label]) => <button className={filter === id ? 'active' : ''} onClick={() => setFilter(id)} key={id}>{label}</button>)}</div><label className="commitment-status-filter"><span>Situação</span><select value={statusFilter} onChange={event => setStatusFilter(event.target.value as typeof statusFilter)}><option value="all">Todas</option><option value="open">Em aberto</option><option value="overdue">Vencidas</option><option value="settled">Liquidadas</option><option value="cancelled">Canceladas</option></select></label><span>{visible.length} registros</span></div>
      <div className="commitment-table-head"><span>Compromisso</span><span>Vencimento</span><span>Situação</span><span>Valor</span><span /></div>
      <div className="commitment-records">{visible.map(item => <CommitmentRow item={item} attachmentCount={attachmentCounts[item.id] ?? 0} onAttachments={() => setAttachmentTarget(item)} onEdit={() => setEditing(item)} onSettle={() => setSettling(item)} onCancel={() => void cancel(item)} key={item.id} />)}{visible.length === 0 && <div className="empty-cashflow"><CalendarDays size={25} /><strong>Nenhum compromisso</strong><span>Cadastre uma conta para começar sua agenda.</span></div>}</div>
    </section>
    {createOpen && <CreateCommitmentModal contacts={contacts} onClose={() => setCreateOpen(false)} onSaved={async count => { setCreateOpen(false); await onChanged(); showToast(count > 1 ? `${count} compromissos agendados` : 'Compromisso criado') }} />}
    {editing && <CreateCommitmentModal contacts={contacts} commitment={editing} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await onChanged(); showToast('Compromisso atualizado') }} />}
    {settling && <SettlementModal item={settling} accounts={accounts} onClose={() => setSettling(null)} onSaved={async () => { setSettling(null); await onChanged(); showToast('Baixa registrada e movimentação criada') }} />}
    {attachmentTarget && <AttachmentModal entityType="Commitment" entityId={attachmentTarget.id} title={attachmentTarget.description} onClose={() => setAttachmentTarget(null)} onChanged={loadAttachmentCounts} showToast={showToast} />}
  </div>
}

function CommitmentRow({ item, attachmentCount, onAttachments, onEdit, onSettle, onCancel }: { item: CommitmentData; attachmentCount: number; onAttachments: () => void; onEdit: () => void; onSettle: () => void; onCancel: () => void }) {
  const labels = { Open: 'Em aberto', PartiallySettled: 'Parcial', Settled: 'Liquidado', Cancelled: 'Cancelado' }
  const due = new Date(`${item.dueDate}T12:00:00`).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })
  const actionable = item.status === 'Open' || item.status === 'PartiallySettled'
  return <div className={`commitment-record ${item.status.toLowerCase()} ${item.isOverdue ? 'is-overdue' : ''}`}><div className="record-title"><span className={item.kind === 'Receivable' ? 'receivable' : 'payable'}>{item.kind === 'Receivable' ? <ArrowDownLeft size={17} /> : <ArrowUpRight size={17} />}</span><div><strong>{item.description}{item.installmentCount > 1 ? ` • ${item.installmentNumber}/${item.installmentCount}` : item.recurrenceFrequency ? ` • ${item.recurrenceNumber}/${item.recurrenceCount}` : ''}</strong><small>{item.contact} · {item.category}{item.recurrenceFrequency ? ` · ${item.recurrenceFrequency === 'Weekly' ? 'Semanal' : item.recurrenceFrequency === 'Monthly' ? 'Mensal' : 'Anual'}` : ''}</small></div></div><div className="record-date"><strong>{due}</strong><small>{item.isOverdue ? 'Vencido' : 'Vencimento'}</small></div><span className={`status-pill ${item.status.toLowerCase()} ${item.isOverdue ? 'overdue' : ''}`}>{item.isOverdue ? 'Vencido' : labels[item.status]}</span><div className="record-value"><strong className={item.kind === 'Receivable' ? 'positive-text' : ''}>{item.kind === 'Receivable' ? '+ ' : '− '}{currency(item.remainingAmount)}</strong>{item.settledAmount > 0 && <small>{currency(item.settledAmount)} baixados</small>}</div><div className="record-actions"><button className={`cancel-button attachment-button ${attachmentCount ? 'has-files' : ''}`} onClick={onAttachments} aria-label="Anexos do compromisso" title="Anexos"><Paperclip size={15} />{attachmentCount > 0 && <em>{attachmentCount}</em>}</button>{actionable && <><button className="cancel-button" onClick={onEdit} aria-label="Editar compromisso" title="Editar"><Pencil size={15} /></button><button className="settle-button" onClick={onSettle}><CheckCircle2 size={14} /> Dar baixa</button><button className="cancel-button" onClick={onCancel} aria-label="Cancelar compromisso"><Ban size={15} /></button></>}</div></div>
}

function AttachmentModal({ entityType, entityId, title, onClose, onChanged, showToast }: { entityType: 'Transaction' | 'Commitment'; entityId: string; title: string; onClose: () => void; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [items, setItems] = useState<AttachmentData[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState('')
  const load = async () => { setLoading(true); try { const response = await apiFetch(`/api/attachments?entityType=${entityType}&entityId=${entityId}`); if (!response.ok) throw new Error(); setItems(await response.json() as AttachmentData[]) } catch { showToast('Não foi possível carregar os anexos') } finally { setLoading(false) } }
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void load() }, [entityType, entityId])
  const upload = async (file?: File) => {
    if (!file) return
    if (file.size > 10_000_000) { showToast('O arquivo deve ter até 10 MB'); return }
    const body = new FormData(); body.append('entityType', entityType); body.append('entityId', entityId); body.append('file', file)
    setBusy('upload')
    try { const response = await apiFetch('/api/attachments', { method: 'POST', body }); const problem = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível enviar o arquivo.'); await load(); await onChanged(); showToast('Anexo enviado') }
    catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setBusy('') }
  }
  const remove = async (item: AttachmentData) => {
    if (!window.confirm(`Excluir o anexo “${item.fileName}”?`)) return
    setBusy(item.id)
    const response = await apiFetch(`/api/attachments/${item.id}`, { method: 'DELETE' })
    if (response.ok) { await load(); await onChanged(); showToast('Anexo excluído') } else showToast('Não foi possível excluir o anexo')
    setBusy('')
  }
  const formatSize = (size: number) => size >= 1_000_000 ? `${(size / 1_000_000).toFixed(1).replace('.', ',')} MB` : `${Math.max(1, Math.round(size / 1000))} KB`
  return <div className="modal-backdrop" onMouseDown={onClose}><section className="entry-modal attachment-modal" role="dialog" aria-modal="true" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>DOCUMENTOS</p><h2>Anexos e comprovantes</h2><span>{title}</span></div><button onClick={onClose} aria-label="Fechar"><X size={20} /></button></div><label className={`attachment-drop ${busy === 'upload' ? 'busy' : ''}`}><Paperclip size={22} /><strong>{busy === 'upload' ? 'Enviando arquivo…' : 'Adicionar comprovante'}</strong><small>PDF, PNG ou JPG · máximo de 10 MB</small><input type="file" accept=".pdf,.png,.jpg,.jpeg,application/pdf,image/png,image/jpeg" disabled={busy === 'upload'} onChange={event => { void upload(event.target.files?.[0]); event.target.value = '' }} /></label><div className="attachment-list">{loading ? <div className="attachment-empty"><Clock3 size={21} /><span>Carregando documentos…</span></div> : items.map(item => <article key={item.id}>{item.contentType.startsWith('image/') ? <img src={`/api/attachments/${item.id}/download`} alt="" /> : <span className="attachment-file-icon">PDF</span>}<div><strong>{item.fileName}</strong><small>{formatSize(item.size)} · enviado em {new Date(item.uploadedAt).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}</small></div><a href={`/api/attachments/${item.id}/download`} target="_blank" rel="noreferrer" title="Visualizar ou baixar">Visualizar</a><button onClick={() => void remove(item)} disabled={busy === item.id} aria-label={`Excluir ${item.fileName}`}><X size={15} /></button></article>)}{!loading && items.length === 0 && <div className="attachment-empty"><Paperclip size={24} /><strong>Nenhum documento anexado</strong><span>Adicione recibos, notas fiscais ou comprovantes.</span></div>}</div><div className="attachment-footer"><span><ShieldCheck size={14} /> Arquivos privados deste espaço</span><button className="secondary-button" onClick={onClose}>Concluir</button></div></section></div>
}

function CreateCommitmentModal({ contacts, commitment, onClose, onSaved }: { contacts: ContactData[]; commitment?: CommitmentData | null; onClose: () => void; onSaved: (count: number) => Promise<void> }) {
  const today = financialDate()
  const [kind, setKind] = useState<'Receivable' | 'Payable'>(commitment?.kind ?? 'Receivable')
  const [description, setDescription] = useState(commitment?.description ?? ''); const [contact, setContact] = useState(commitment?.contact ?? ''); const [contactId, setContactId] = useState(commitment?.contactId ?? ''); const [category, setCategory] = useState(commitment?.category ?? 'Receita de serviços'); const [amount, setAmount] = useState(commitment ? commitment.totalAmount.toFixed(2).replace('.', ',') : ''); const [dueDate, setDueDate] = useState(commitment?.dueDate ?? today); const [competenceDate, setCompetenceDate] = useState(commitment?.competenceDate ?? today); const [installments, setInstallments] = useState(1); const [recurrenceFrequency, setRecurrenceFrequency] = useState<'' | 'Weekly' | 'Monthly' | 'Yearly'>(''); const [recurrenceCount, setRecurrenceCount] = useState(12); const [error, setError] = useState(''); const [saving, setSaving] = useState(false)
  const compatibleContacts = contacts.filter(item => item.isActive && (item.kind === 'Both' || (kind === 'Receivable' ? item.kind === 'Customer' : item.kind === 'Supplier')))
  const submit = async (event: React.FormEvent) => { event.preventDefault(); const value = Number(amount.replace(/\./g, '').replace(',', '.')); setSaving(true); setError(''); try { const response = await apiFetch(commitment ? `/api/commitments/${commitment.id}` : '/api/commitments', { method: commitment ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ contactId: contactId || null, description, contact, category, kind, totalAmount: value, dueDate, competenceDate, installments: recurrenceFrequency ? 1 : installments, recurrenceFrequency: recurrenceFrequency || null, recurrenceCount: recurrenceFrequency ? recurrenceCount : 1 }) }); if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(problem?.detail ?? 'Não foi possível salvar.') } const result = await response.json() as CommitmentData | CommitmentData[]; await onSaved(Array.isArray(result) ? result.length : 1) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.'); setSaving(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal commitment-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>Agenda financeira</p><h2>{commitment ? 'Editar compromisso' : 'Novo compromisso'}</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div><div className="entry-types"><button type="button" className={kind === 'Receivable' ? 'selected' : ''} onClick={() => { setKind('Receivable'); setCategory('Receita de serviços'); setContactId('') }}><ArrowDownLeft size={18} /> A receber</button><button type="button" className={kind === 'Payable' ? 'selected' : ''} onClick={() => { setKind('Payable'); setCategory('Operacional'); setContactId('') }}><ArrowUpRight size={18} /> A pagar</button></div><label>Descrição<input value={description} onChange={event => setDescription(event.target.value)} placeholder="Ex.: Projeto de identidade visual" autoFocus /></label><div className="field-row"><label>Cliente ou fornecedor<select value={contactId} onChange={event => { const id = event.target.value; setContactId(id); if (id) setContact(compatibleContacts.find(item => item.id === id)?.name ?? '') }}><option value="">Informar manualmente</option>{compatibleContacts.map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select>{!contactId && <input value={contact} onChange={event => setContact(event.target.value)} placeholder="Nome do contato" />}</label><label>Categoria<input value={category} onChange={event => setCategory(event.target.value)} /></label></div><div className="field-row three"><label>{recurrenceFrequency ? 'Valor por ocorrência' : 'Valor total'}<input value={amount} onChange={event => setAmount(event.target.value)} inputMode="decimal" placeholder="R$ 0,00" /></label><label>{commitment ? 'Vencimento' : 'Primeiro vencimento'}<input type="date" value={dueDate} onChange={event => setDueDate(event.target.value)} /></label>{!commitment && <label>Parcelas<input type="number" min="1" max="48" value={installments} disabled={Boolean(recurrenceFrequency)} onChange={event => setInstallments(Number(event.target.value))} /></label>}</div><div className="field-row three"><label>Competência<input type="date" value={competenceDate} onChange={event => setCompetenceDate(event.target.value)} /></label>{!commitment && <><label>Repetir<select value={recurrenceFrequency} onChange={event => { setRecurrenceFrequency(event.target.value as typeof recurrenceFrequency); if (event.target.value) setInstallments(1) }}><option value="">Não repetir</option><option value="Weekly">Toda semana</option><option value="Monthly">Todo mês</option><option value="Yearly">Todo ano</option></select></label><label>Ocorrências<input type="number" min="2" max="60" value={recurrenceCount} disabled={!recurrenceFrequency} onChange={event => setRecurrenceCount(Number(event.target.value))} /></label></>}</div>{commitment && (commitment.installmentCount > 1 || commitment.recurrenceFrequency) ? <p className="installment-hint"><Pencil size={14} /> A alteração vale somente para este compromisso da série.</p> : recurrenceFrequency ? <p className="installment-hint"><Repeat2 size={14} /> Serão criados {recurrenceCount} compromissos de {currency(Number(amount.replace(/\./g, '').replace(',', '.')) || 0)}.</p> : installments > 1 && <p className="installment-hint">Serão criadas {installments} parcelas mensais de aproximadamente {currency((Number(amount.replace(/\./g, '').replace(',', '.')) || 0) / installments)}.</p>}{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Salvando…' : commitment ? 'Salvar alterações' : recurrenceFrequency ? 'Criar recorrência' : 'Criar compromisso'}</button></div></form></div>
}

function SettlementModal({ item, accounts, onClose, onSaved }: { item: CommitmentData; accounts: Account[]; onClose: () => void; onSaved: () => Promise<void> }) {
  const [amount, setAmount] = useState(item.remainingAmount.toFixed(2).replace('.', ',')); const [accountId, setAccountId] = useState(accounts[0]?.id ?? ''); const [date, setDate] = useState(financialDate()); const [error, setError] = useState(''); const [saving, setSaving] = useState(false)
  const submit = async (event: React.FormEvent) => { event.preventDefault(); setSaving(true); setError(''); try { const value = Number(amount.replace(/\./g, '').replace(',', '.')); const response = await apiFetch(`/api/commitments/${item.id}/settlements`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ accountId, amount: value, occurredOn: date }) }); if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(problem?.detail ?? 'Não foi possível dar baixa.') } await onSaved() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.'); setSaving(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal settlement-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>{item.kind === 'Receivable' ? 'Recebimento' : 'Pagamento'}</p><h2>Dar baixa</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div><div className="settlement-summary"><span>{item.description}</span><strong>Restante: {currency(item.remainingAmount)}</strong></div><div className="field-row"><label>Valor da baixa<input value={amount} onChange={event => setAmount(event.target.value)} inputMode="decimal" autoFocus /></label><label>Data<input type="date" value={date} onChange={event => setDate(event.target.value)} /></label></div><label>Conta<select value={accountId} onChange={event => setAccountId(event.target.value)}>{accounts.map(account => <option value={account.id} key={account.id}>{account.name}</option>)}</select></label><p className="settlement-note"><CheckCircle2 size={14} /> Uma movimentação será criada automaticamente.</p>{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Salvando…' : 'Confirmar baixa'}</button></div></form></div>
}

function GoalsPage({ data, categories, onChanged, showToast }: { data: GoalsSummaryData | null; categories: CategoryData[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [filter, setFilter] = useState<'active' | 'risk' | 'completed' | 'all'>('active')
  const [editing, setEditing] = useState<FinancialGoalData | 'new' | null>(null)
  const [contributing, setContributing] = useState<FinancialGoalData | null>(null)
  const goals = data?.goals.filter(item => filter === 'all' || (filter === 'active' && item.isActive) || (filter === 'risk' && ['AtRisk', 'Overdue', 'Exceeded'].includes(item.status)) || (filter === 'completed' && item.status === 'Completed')) ?? []
  const statusLabel = (status: FinancialGoalData['status']) => ({ OnTrack: 'No ritmo', AtRisk: 'Atenção', Overdue: 'Atrasada', Exceeded: 'Limite excedido', Completed: 'Concluída', Archived: 'Arquivada' })[status]
  const kindLabel = (kind: FinancialGoalData['kind']) => ({ Revenue: 'Faturamento', ExpenseLimit: 'Limite de despesas', EmergencyReserve: 'Reserva', CashBalance: 'Saldo de caixa', Custom: 'Personalizada' })[kind]
  const setActive = async (goal: FinancialGoalData) => { const response = await apiFetch(`/api/goals/${goal.id}/active`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ active: !goal.isActive }) }); if (!response.ok) { showToast('Não foi possível alterar a meta.'); return } await onChanged(); showToast(goal.isActive ? 'Meta arquivada' : 'Meta reativada') }
  return <div className="page goals-page"><section className="page-heading goals-heading"><div><p className="eyebrow">PLANEJAMENTO</p><h1>Metas financeiras</h1><p>Transforme prioridades financeiras em objetivos acompanháveis.</p></div><button className="primary-button" onClick={() => setEditing('new')}><Plus size={17} /> Nova meta</button></section>
    <section className="goal-kpis"><article><span className="kpi-symbol balance"><Goal size={18} /></span><div><small>Metas ativas</small><strong>{data?.activeCount ?? 0}</strong></div></article><article><span className="kpi-symbol receivable"><CheckCircle2 size={18} /></span><div><small>Concluídas</small><strong>{data?.completedCount ?? 0}</strong></div></article><article className={(data?.atRiskCount ?? 0) ? 'risk' : ''}><span className="kpi-symbol overdue"><Bell size={18} /></span><div><small>Precisam de atenção</small><strong>{data?.atRiskCount ?? 0}</strong></div></article><article><span className="kpi-symbol receivable"><TrendingUp size={18} /></span><div><small>Progresso acumulado</small><strong>{currency(data?.totalCurrentAmount ?? 0)}</strong></div></article></section>
    <section className="goals-panel"><div className="goals-toolbar"><div className="commitment-tabs">{([['active', 'Ativas'], ['risk', 'Em atenção'], ['completed', 'Concluídas'], ['all', 'Todas']] as const).map(([id, label]) => <button className={filter === id ? 'active' : ''} onClick={() => setFilter(id)} key={id}>{label}</button>)}</div><span>{goals.length} metas</span></div><div className="goal-grid">{goals.map(goal => <article className={`goal-card ${goal.status.toLowerCase()} ${!goal.isActive ? 'inactive' : ''}`} key={goal.id}><div className="goal-card-head"><span><Goal size={18} /></span><div><small>{kindLabel(goal.kind)}{goal.category ? ` • ${goal.category}` : ''}</small><h3>{goal.name}</h3></div><em>{statusLabel(goal.status)}</em></div><div className="goal-numbers"><div><small>{goal.kind === 'ExpenseLimit' ? 'Utilizado' : 'Alcançado'}</small><strong>{currency(goal.currentAmount)}</strong></div><div><small>{goal.kind === 'ExpenseLimit' ? 'Limite' : 'Meta'}</small><strong>{currency(goal.targetAmount)}</strong></div></div><div className="goal-progress"><div><i style={{ width: `${Math.min(goal.progressPercentage, 100)}%` }} /></div><span>{goal.progressPercentage.toLocaleString('pt-BR')}%</span></div><div className="goal-deadline"><CalendarDays size={14} /><span>Prazo: <strong>{new Date(`${goal.dueDate}T12:00:00`).toLocaleDateString('pt-BR')}</strong>{goal.projectedCompletionDate && goal.status !== 'Completed' ? ` • projeção ${new Date(`${goal.projectedCompletionDate}T12:00:00`).toLocaleDateString('pt-BR')}` : ''}</span></div><div className="goal-card-actions">{goal.isActive && (goal.kind === 'EmergencyReserve' || goal.kind === 'Custom') && <button onClick={() => setContributing(goal)}><Plus size={14} /> Aportar</button>}<button onClick={() => setEditing(goal)}>Editar</button><button onClick={() => void setActive(goal)}>{goal.isActive ? 'Arquivar' : 'Reativar'}</button></div></article>)}{!goals.length && <div className="goal-empty"><Goal size={30} /><strong>Nenhuma meta encontrada</strong><span>Crie uma meta ou escolha outro filtro.</span></div>}</div></section>
    {editing && <GoalModal goal={editing === 'new' ? null : editing} categories={categories} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await onChanged(); showToast('Meta salva') }} />}
    {contributing && <GoalContributionModal goal={contributing} onClose={() => setContributing(null)} onSaved={async () => { setContributing(null); await onChanged(); showToast('Aporte registrado') }} />}
  </div>
}

function GoalModal({ goal, categories, onClose, onSaved }: { goal: FinancialGoalData | null; categories: CategoryData[]; onClose: () => void; onSaved: () => Promise<void> }) {
  const today = financialDate(); const nextYear = new Date(); nextYear.setFullYear(nextYear.getFullYear() + 1)
  const [name, setName] = useState(goal?.name ?? ''); const [kind, setKind] = useState<FinancialGoalData['kind']>(goal?.kind ?? 'Revenue'); const [amount, setAmount] = useState(goal?.targetAmount.toString() ?? ''); const [startDate, setStartDate] = useState(goal?.startDate ?? today); const [dueDate, setDueDate] = useState(goal?.dueDate ?? financialDate(nextYear)); const [category, setCategory] = useState(goal?.category ?? ''); const [saving, setSaving] = useState(false); const [error, setError] = useState('')
  const categoryOptions = categories.filter(item => item.isActive && (item.kind === (kind === 'Revenue' ? 'Income' : 'Expense') || item.kind === 'Both'))
  const submit = async (event: React.FormEvent) => { event.preventDefault(); setSaving(true); setError(''); try { const response = await apiFetch(goal ? `/api/goals/${goal.id}` : '/api/goals', { method: goal ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, kind, targetAmount: Number(amount.replace(',', '.')), startDate, dueDate, category: ['Revenue', 'ExpenseLimit'].includes(kind) ? category || null : null }) }); const problem = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível salvar a meta.'); await onSaved() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal goal-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>PLANEJAMENTO</p><h2>{goal ? 'Editar meta' : 'Nova meta financeira'}</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div><label>Nome da meta<input value={name} onChange={event => setName(event.target.value)} placeholder="Ex.: Reserva para seis meses" autoFocus /></label><label>Tipo<select value={kind} onChange={event => { setKind(event.target.value as FinancialGoalData['kind']); setCategory('') }}><option value="Revenue">Faturamento</option><option value="ExpenseLimit">Limite de despesas</option><option value="EmergencyReserve">Reserva de emergência</option><option value="CashBalance">Saldo de caixa</option><option value="Custom">Personalizada</option></select></label>{(kind === 'Revenue' || kind === 'ExpenseLimit') && <label>Categoria opcional<select value={category} onChange={event => setCategory(event.target.value)}><option value="">Todas as categorias compatíveis</option>{categoryOptions.map(item => <option value={item.name} key={item.id}>{item.name}</option>)}</select></label>}<div className="field-row"><label>Valor-alvo<input value={amount} onChange={event => setAmount(event.target.value)} inputMode="decimal" placeholder="R$ 0,00" /></label><label>Data inicial<input type="date" value={startDate} onChange={event => setStartDate(event.target.value)} /></label></div><label>Prazo<input type="date" value={dueDate} onChange={event => setDueDate(event.target.value)} /></label><p className="goal-modal-note"><Sparkles size={15} /> O progresso de faturamento, despesas e caixa é atualizado pelos lançamentos. Reservas e metas personalizadas recebem aportes manuais.</p>{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Salvando…' : 'Salvar meta'}</button></div></form></div>
}

function GoalContributionModal({ goal, onClose, onSaved }: { goal: FinancialGoalData; onClose: () => void; onSaved: () => Promise<void> }) {
  const [amount, setAmount] = useState(''); const [date, setDate] = useState(financialDate()); const [note, setNote] = useState(''); const [saving, setSaving] = useState(false); const [error, setError] = useState('')
  const submit = async (event: React.FormEvent) => { event.preventDefault(); setSaving(true); setError(''); try { const response = await apiFetch(`/api/goals/${goal.id}/contributions`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ amount: Number(amount.replace(',', '.')), contributedOn: date, note }) }); const problem = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível registrar o aporte.'); await onSaved() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal contribution-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>APORTE</p><h2>{goal.name}</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div><div className="contribution-summary"><span>Progresso atual</span><strong>{currency(goal.currentAmount)} de {currency(goal.targetAmount)}</strong></div><div className="field-row"><label>Valor do aporte<input value={amount} onChange={event => setAmount(event.target.value)} inputMode="decimal" placeholder="R$ 0,00" autoFocus /></label><label>Data<input type="date" value={date} onChange={event => setDate(event.target.value)} /></label></div><label>Observação<input value={note} maxLength={160} onChange={event => setNote(event.target.value)} placeholder="Ex.: Aporte mensal" /></label>{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving}>{saving ? 'Registrando…' : 'Registrar aporte'}</button></div></form></div>
}

function ReportsPage({ centers, showToast }: { centers: CostCenterData[]; showToast: (value: string) => void }) {
  const [view, setView] = useState<'dre' | 'budget'>('dre')
  const switcher = <div className="report-view-tabs"><button className={view === 'dre' ? 'active' : ''} onClick={() => setView('dre')}><FileBarChart size={16} /> DRE mensal</button><button className={view === 'budget' ? 'active' : ''} onClick={() => setView('budget')}><CircleDollarSign size={16} /> Orçado × realizado</button></div>
  return view === 'dre' ? <DreReportPage centers={centers} showToast={showToast} switcher={switcher} /> : <BudgetVariancePage showToast={showToast} switcher={switcher} />
}

function DreReportPage({ centers, showToast, switcher }: { centers: CostCenterData[]; showToast: (value: string) => void; switcher: React.ReactNode }) {
  const now = new Date()
  const [period, setPeriod] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const [centerId, setCenterId] = useState('')
  const [year, month] = period.split('-').map(Number)
  const params = new URLSearchParams({ year: String(year), month: String(month) })
  if (centerId) params.set('costCenterId', centerId)
  const { data: report, loading } = useApiQuery<DreData>(
    `/api/reports/dre?${params}`, () => showToast('Não foi possível carregar a DRE.'),
  )
  const monthLabel = new Date(year, month - 1, 1).toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })
  const variation = (value: number) => `${value > 0 ? '+' : ''}${value.toLocaleString('pt-BR')}%`
  const downloadCsv = () => {
    if (!report) return
    const rows = [['DRE gerencial', monthLabel], ['Centro de custo', report.costCenterName ?? 'Todos'], [], ['Descrição', 'Período atual', 'Período anterior', 'Variação'], ['RECEITA BRUTA', report.grossRevenue, report.previousGrossRevenue, ''], ...report.revenueLines.map(line => [`  ${line.category}`, line.currentAmount, line.previousAmount, `${line.variationPercentage}%`]), ['DESPESAS OPERACIONAIS', report.operatingExpenses, report.previousOperatingExpenses, ''], ...report.expenseLines.map(line => [`  ${line.category}`, line.currentAmount, line.previousAmount, `${line.variationPercentage}%`]), ['RESULTADO LÍQUIDO', report.netResult, report.previousNetResult, `${report.resultVariationPercentage}%`], ['MARGEM', `${report.marginPercentage}%`, '', '']]
    const content = rows.map(row => row.map(value => `"${String(value).replaceAll('"', '""')}"`).join(';')).join('\n')
    const url = URL.createObjectURL(new Blob([`\uFEFF${content}`], { type: 'text/csv;charset=utf-8' })); const link = document.createElement('a'); link.href = url; link.download = `dre-${period}.csv`; link.click(); URL.revokeObjectURL(url)
    showToast('DRE exportada em CSV')
  }
  const reportRow = (line: DreLineData) => <div className="dre-row detail" key={`${line.kind}-${line.category}`}><span>{line.category}</span><strong>{currency(line.currentAmount)}</strong><span>{currency(line.previousAmount)}</span><em className={line.variationPercentage > 0 ? 'up' : line.variationPercentage < 0 ? 'down' : ''}>{variation(line.variationPercentage)}</em></div>
  return <div className="page reports-page">
    <section className="page-heading reports-heading"><div><p className="eyebrow">ANÁLISE GERENCIAL</p><h1>DRE mensal</h1><p>Entenda receitas, despesas, resultado e margem dos lançamentos confirmados.</p></div><div className="report-actions"><button className="secondary-button" onClick={downloadCsv} disabled={!report}><Download size={16} /> Exportar CSV</button><button className="secondary-button" onClick={() => window.print()}><Printer size={16} /> Imprimir / PDF</button></div></section>
    {switcher}
    <section className="report-filters"><label>Período<input type="month" value={period} onChange={event => setPeriod(event.target.value)} /></label><label>Centro de custo<select value={centerId} onChange={event => setCenterId(event.target.value)}><option value="">Todos os centros</option>{centers.filter(item => item.isActive).map(item => <option value={item.id} key={item.id}>{item.code} • {item.name}</option>)}</select></label><div><small>Visualização</small><strong>{monthLabel}{report?.costCenterName ? ` • ${report.costCenterName}` : ''}</strong></div></section>
    <section className="dre-kpis"><article><span className="kpi-symbol receivable"><TrendingUp size={18} /></span><div><small>Receita bruta</small><strong>{currency(report?.grossRevenue ?? 0)}</strong><em>{variation(report?.previousGrossRevenue ? ((report?.grossRevenue ?? 0) - report.previousGrossRevenue) / report.previousGrossRevenue * 100 : 0)} vs. mês anterior</em></div></article><article><span className="kpi-symbol payable"><TrendingDown size={18} /></span><div><small>Despesas operacionais</small><strong>{currency(report?.operatingExpenses ?? 0)}</strong><em>{currency(report?.previousOperatingExpenses ?? 0)} no mês anterior</em></div></article><article className={(report?.netResult ?? 0) < 0 ? 'negative' : ''}><span className="kpi-symbol balance"><CircleDollarSign size={18} /></span><div><small>Resultado líquido</small><strong>{(report?.netResult ?? 0) < 0 ? '− ' : ''}{currency(report?.netResult ?? 0)}</strong><em>{variation(report?.resultVariationPercentage ?? 0)} vs. mês anterior</em></div></article><article><span className="kpi-symbol receivable"><PieChart size={18} /></span><div><small>Margem</small><strong>{(report?.marginPercentage ?? 0).toLocaleString('pt-BR')}%</strong><em>sobre a receita do período</em></div></article></section>
    <section className="dre-panel"><div className="dre-panel-head"><div><h3>Demonstração do resultado</h3><p>Transferências internas são desconsideradas no resultado.</p></div><span>Valores em BRL</span></div><div className="dre-columns"><span>Descrição</span><span>Atual</span><span>Anterior</span><span>Variação</span></div>
      {loading ? <div className="dre-empty">Calculando resultado…</div> : <><div className="dre-row total income"><span>Receita bruta</span><strong>{currency(report?.grossRevenue ?? 0)}</strong><span>{currency(report?.previousGrossRevenue ?? 0)}</span><em /></div>{report?.revenueLines.map(reportRow)}<div className="dre-row total expense"><span>Despesas operacionais</span><strong>− {currency(report?.operatingExpenses ?? 0)}</strong><span>− {currency(report?.previousOperatingExpenses ?? 0)}</span><em /></div>{report?.expenseLines.map(reportRow)}<div className={`dre-row result ${(report?.netResult ?? 0) < 0 ? 'negative' : ''}`}><span>Resultado líquido</span><strong>{(report?.netResult ?? 0) < 0 ? '− ' : ''}{currency(report?.netResult ?? 0)}</strong><span>{(report?.previousNetResult ?? 0) < 0 ? '− ' : ''}{currency(report?.previousNetResult ?? 0)}</span><em>{variation(report?.resultVariationPercentage ?? 0)}</em></div></>}
    </section>
  </div>
}

function BudgetVariancePage({ showToast, switcher }: { showToast: (value: string) => void; switcher: React.ReactNode }) {
  const now = new Date()
  const [period, setPeriod] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const [months, setMonths] = useState(6)
  const [kind, setKind] = useState<'all' | 'Category' | 'CostCenter'>('all')
  const [status, setStatus] = useState<'all' | 'Healthy' | 'Warning' | 'Exceeded'>('all')
  const [year, month] = period.split('-').map(Number)
  const { data: report, loading } = useApiQuery<BudgetVarianceData>(
    `/api/reports/budget-variance?year=${year}&month=${month}&months=${months}`,
    () => showToast('Não foi possível carregar o comparativo.'),
  )
  const lines = report?.lines.filter(line => (kind === 'all' || line.targetKind === kind) && (status === 'all' || line.status === status)) ?? []
  const statusLabel = (value: BudgetVarianceLineData['status']) => value === 'Exceeded' ? 'Excedido' : value === 'Warning' ? 'Atenção' : 'Saudável'
  const maxPeriod = Math.max(1, ...(report?.periods.flatMap(item => [item.plannedAmount, item.projectedAmount]) ?? [1]))
  const exportCsv = () => {
    if (!report) return
    const rows = [['Orçado x realizado', period], [], ['Destino', 'Tipo', 'Planejado', 'Realizado', 'Comprometido', 'Projetado', 'Desvio', 'Desvio %', 'Situação'], ...lines.map(line => [line.targetName, line.targetKind, line.plannedAmount, line.actualAmount, line.committedAmount, line.projectedAmount, line.deviationAmount, `${line.deviationPercentage}%`, statusLabel(line.status)])]
    const content = rows.map(row => row.map(value => `"${String(value).replaceAll('"', '""')}"`).join(';')).join('\n'); const url = URL.createObjectURL(new Blob([`\uFEFF${content}`], { type: 'text/csv;charset=utf-8' })); const link = document.createElement('a'); link.href = url; link.download = `orcado-realizado-${period}.csv`; link.click(); URL.revokeObjectURL(url); showToast('Comparativo exportado em CSV')
  }
  return <div className="page reports-page variance-page">
    <section className="page-heading reports-heading"><div><p className="eyebrow">CONTROLE ORÇAMENTÁRIO</p><h1>Orçado × realizado</h1><p>Identifique desvios antes que os limites financeiros sejam ultrapassados.</p></div><div className="report-actions"><button className="secondary-button" onClick={exportCsv} disabled={!report}><Download size={16} /> Exportar CSV</button><button className="secondary-button" onClick={() => window.print()}><Printer size={16} /> Imprimir / PDF</button></div></section>
    {switcher}
    <section className="report-filters variance-filters"><label>Período<input type="month" value={period} onChange={event => setPeriod(event.target.value)} /></label><label>Tipo<select value={kind} onChange={event => setKind(event.target.value as typeof kind)}><option value="all">Todos</option><option value="Category">Categorias</option><option value="CostCenter">Centros de custo</option></select></label><label>Situação<select value={status} onChange={event => setStatus(event.target.value as typeof status)}><option value="all">Todas</option><option value="Healthy">Saudável</option><option value="Warning">Atenção</option><option value="Exceeded">Excedido</option></select></label><label>Histórico<select value={months} onChange={event => setMonths(Number(event.target.value))}><option value="3">3 meses</option><option value="6">6 meses</option><option value="12">12 meses</option></select></label></section>
    <section className="budget-kpis variance-kpis"><article><small>Planejado</small><strong>{currency(report?.plannedAmount ?? 0)}</strong><span>limite consolidado</span></article><article><small>Realizado</small><strong>{currency(report?.actualAmount ?? 0)}</strong><span>já confirmado</span></article><article><small>Comprometido</small><strong>{currency(report?.committedAmount ?? 0)}</strong><span>a pagar em aberto</span></article><article className={(report?.deviationAmount ?? 0) < 0 ? 'danger' : 'available'}><small>Desvio disponível</small><strong>{(report?.deviationAmount ?? 0) < 0 ? '− ' : ''}{currency(report?.deviationAmount ?? 0)}</strong><span>{(report?.executionPercentage ?? 0).toLocaleString('pt-BR')}% projetado</span></article></section>
    <section className="variance-history"><div className="budget-list-head"><div><h3>Evolução mensal</h3><p>Planejado comparado ao total projetado.</p></div><span>{months} meses</span></div><div className="variance-chart">{report?.periods.map(item => <div className="variance-month" key={`${item.year}-${item.month}`}><div className="variance-bars"><i className="planned" style={{ height: `${item.plannedAmount / maxPeriod * 100}%` }} /><i className={`projected ${item.projectedAmount > item.plannedAmount ? 'over' : ''}`} style={{ height: `${item.projectedAmount / maxPeriod * 100}%` }} /></div><span>{new Date(item.year, item.month - 1, 1).toLocaleDateString('pt-BR', { month: 'short' }).replace('.', '')}</span></div>)}</div><div className="variance-legend"><span><i className="planned" /> Planejado</span><span><i className="projected" /> Projetado</span></div></section>
    <section className="variance-table"><div className="budget-list-head"><div><h3>Desvios por destino</h3><p>Comprometido representa contas a pagar ainda não liquidadas.</p></div><span>{lines.length} resultados</span></div><div className="variance-columns"><span>Categoria / centro</span><span>Planejado</span><span>Realizado</span><span>Comprometido</span><span>Projetado</span><span>Desvio</span><span>Situação</span></div>{loading ? <div className="dre-empty">Calculando comparativo…</div> : lines.length ? lines.map(line => <div className={`variance-row ${line.status.toLowerCase()}`} key={line.budgetId}><div><strong>{line.targetName}</strong><small>{line.targetKind === 'Category' ? 'Categoria' : `Centro • ${line.targetCode}`}</small></div><span>{currency(line.plannedAmount)}</span><span>{currency(line.actualAmount)}</span><span>{currency(line.committedAmount)}</span><span>{currency(line.projectedAmount)}</span><span className={line.deviationAmount < 0 ? 'negative' : 'positive'}>{line.deviationAmount < 0 ? '− ' : '+ '}{currency(line.deviationAmount)}<small>{line.deviationPercentage > 0 ? '+' : ''}{line.deviationPercentage.toLocaleString('pt-BR')}%</small></span><em className={line.status.toLowerCase()}>{statusLabel(line.status)}</em></div>) : <div className="dre-empty"><CircleDollarSign size={26} /><strong>Nenhum limite encontrado</strong><span>Revise os filtros ou cadastre um orçamento para o mês.</span></div>}</section>
  </div>
}

function BudgetPage({ categories, centers, onCurrentChanged, showToast }: { categories: CategoryData[]; centers: CostCenterData[]; onCurrentChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const now = new Date()
  const [period, setPeriod] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const [editing, setEditing] = useState<BudgetLineData | 'new' | null>(null)
  const [revision, setRevision] = useState(0)
  const [year, month] = period.split('-').map(Number)

  const { data: summary, loading } = useApiQuery<BudgetSummaryData>(
    `/api/budgets?year=${year}&month=${month}`,
    () => showToast('Não foi possível carregar o orçamento.'), revision,
  )

  const remove = async (line: BudgetLineData) => {
    if (!window.confirm(`Excluir o orçamento de “${line.targetName}”?`)) return
    const response = await apiFetch(`/api/budgets/${line.id}`, { method: 'DELETE' })
    if (!response.ok) { showToast('Não foi possível excluir o orçamento.'); return }
    setRevision(value => value + 1); await onCurrentChanged(); showToast('Orçamento excluído')
  }
  const statusLabel = (status: BudgetLineData['status']) => status === 'Exceeded' ? 'Limite excedido' : status === 'Warning' ? 'Atenção' : 'Dentro do limite'

  return <div className="page budgets-page">
    <section className="page-heading budget-heading"><div><p className="eyebrow">PLANEJAMENTO</p><h1>Orçamento mensal</h1><p>Compare limites, gastos realizados e compromissos ainda previstos.</p></div><div className="budget-actions"><input type="month" value={period} onChange={event => setPeriod(event.target.value)} /><button className="primary-button" onClick={() => setEditing('new')}><Plus size={17} /> Novo limite</button></div></section>
    <section className="budget-kpis"><article><small>Planejado</small><strong>{currency(summary?.plannedAmount ?? 0)}</strong><span>limites do período</span></article><article><small>Realizado</small><strong>{currency(summary?.actualAmount ?? 0)}</strong><span>despesas confirmadas</span></article><article><small>Projetado</small><strong>{currency(summary?.projectedAmount ?? 0)}</strong><span>realizado + contas a pagar</span></article><article className={(summary?.availableAmount ?? 0) < 0 ? 'danger' : 'available'}><small>Disponível</small><strong>{(summary?.availableAmount ?? 0) < 0 ? '− ' : ''}{currency(summary?.availableAmount ?? 0)}</strong><span>{summary?.alertCount ?? 0} alertas ativos</span></article></section>
    <section className="budget-list-panel"><div className="budget-list-head"><div><h3>Limites por categoria e centro</h3><p>A projeção de categorias inclui compromissos a pagar ainda em aberto.</p></div><span>{summary?.lines.length ?? 0} limites</span></div>
      {loading && <div className="budget-empty">Carregando orçamento…</div>}
      {!loading && !summary?.lines.length && <div className="budget-empty"><CircleDollarSign size={28} /><strong>Nenhum limite neste mês</strong><span>Crie o primeiro para começar o acompanhamento.</span></div>}
      {!loading && summary?.lines.map(line => <article className={`budget-line ${line.status.toLowerCase()}`} key={line.id}>
        <div className="budget-line-title"><span>{line.targetKind === 'Category' ? <Tags size={17} /> : <Building2 size={17} />}</span><div><strong>{line.targetName}</strong><small>{line.targetKind === 'Category' ? 'Categoria' : `Centro de custo • ${line.targetCode}`}</small></div></div>
        <div className="budget-line-values"><div><small>Realizado</small><strong>{currency(line.actualAmount)}</strong></div><div><small>Projetado</small><strong>{currency(line.projectedAmount)}</strong></div><div><small>Limite</small><strong>{currency(line.plannedAmount)}</strong></div></div>
        <div className="budget-line-progress"><div><i style={{ width: `${Math.min(line.usagePercentage, 100)}%` }} /></div><span>{line.usagePercentage.toLocaleString('pt-BR')}%</span></div>
        <span className={`budget-status ${line.status.toLowerCase()}`}>{statusLabel(line.status)}</span>
        <div className="budget-line-actions"><button className="secondary-button" onClick={() => setEditing(line)}>Editar</button><button className="icon-button" onClick={() => void remove(line)} aria-label={`Excluir orçamento de ${line.targetName}`}><X size={16} /></button></div>
      </article>)}
    </section>
    {editing && <BudgetModal line={editing === 'new' ? null : editing} year={year} month={month} categories={categories} centers={centers} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); setRevision(value => value + 1); await onCurrentChanged(); showToast('Orçamento salvo') }} />}
  </div>
}

function BudgetModal({ line, year, month, categories, centers, onClose, onSaved }: { line: BudgetLineData | null; year: number; month: number; categories: CategoryData[]; centers: CostCenterData[]; onClose: () => void; onSaved: () => Promise<void> }) {
  const [targetKind, setTargetKind] = useState<'Category' | 'CostCenter'>(line?.targetKind ?? 'Category')
  const categoryOptions = categories.filter(item => item.isActive && (item.kind === 'Expense' || item.kind === 'Both'))
  const centerOptions = centers.filter(item => item.isActive)
  const options = targetKind === 'Category' ? categoryOptions : centerOptions
  const [targetId, setTargetId] = useState(line?.targetId ?? categoryOptions[0]?.id ?? '')
  const [amount, setAmount] = useState(line?.plannedAmount.toString() ?? '')
  const [alert, setAlert] = useState(line?.alertPercentage.toString() ?? '80')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const changeKind = (kind: 'Category' | 'CostCenter') => { setTargetKind(kind); setTargetId((kind === 'Category' ? categoryOptions : centerOptions)[0]?.id ?? '') }
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    const plannedAmount = Number(amount.replace(',', '.')); const alertPercentage = Number(alert)
    try {
      const response = await apiFetch('/api/budgets', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ year, month, targetKind, targetId, plannedAmount, alertPercentage }) })
      const problem = await response.json().catch(() => null) as { detail?: string } | null
      if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível salvar o orçamento.')
      await onSaved()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) }
  }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal budget-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>PLANEJAMENTO</p><h2>{line ? 'Editar limite' : 'Novo limite mensal'}</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div>
    <div className="entry-types"><button type="button" className={targetKind === 'Category' ? 'selected' : ''} onClick={() => changeKind('Category')} disabled={Boolean(line)}><Tags size={17} /> Categoria</button><button type="button" className={targetKind === 'CostCenter' ? 'selected' : ''} onClick={() => changeKind('CostCenter')} disabled={Boolean(line)}><Building2 size={17} /> Centro de custo</button></div>
    <label>Aplicar limite a<select value={targetId} onChange={event => setTargetId(event.target.value)} disabled={Boolean(line)}>{options.map(item => <option value={item.id} key={item.id}>{'code' in item ? `${item.code} • ` : ''}{item.name}</option>)}</select></label>
    <div className="field-row"><label>Valor planejado<input value={amount} onChange={event => setAmount(event.target.value)} inputMode="decimal" placeholder="R$ 0,00" autoFocus /></label><label>Alertar ao atingir (%)<input type="number" min="1" max="100" value={alert} onChange={event => setAlert(event.target.value)} /></label></div>
    <p className="budget-modal-note"><Bell size={15} /> O alerta muda a situação visual quando a projeção atinge o percentual definido.</p>{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving || !targetId}>{saving ? 'Salvando…' : 'Salvar limite'}</button></div>
  </form></div>
}

function StructurePage({ categories, centers, accounts, transactions, onChanged, showToast }: { categories: CategoryData[]; centers: CostCenterData[]; accounts: Account[]; transactions: Transaction[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [tab, setTab] = useState<'categories' | 'centers' | 'allocations' | 'rules'>('categories')
  const [categoryName, setCategoryName] = useState(''); const [categoryKind, setCategoryKind] = useState<'Income' | 'Expense' | 'Both'>('Expense'); const [parentId, setParentId] = useState('')
  const [centerName, setCenterName] = useState(''); const [centerCode, setCenterCode] = useState('')
  const [allocations, setAllocations] = useState<AllocationData[]>([]); const [allocating, setAllocating] = useState<Transaction | null>(null); const [saving, setSaving] = useState(false)

  const loadAllocations = async () => { try { const response = await apiFetch('/api/allocations'); if (response.ok) setAllocations(await response.json() as AllocationData[]) } catch { /* feedback is handled by the page state */ } }
  // A lista de rateios acompanha as mudanças persistidas na estrutura financeira.
  // oxlint-disable-next-line react/set-state-in-effect
  useEffect(() => { void loadAllocations() }, [transactions])

  const createCategory = async (event: React.FormEvent) => { event.preventDefault(); setSaving(true); try { const response = await apiFetch('/api/categories', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: categoryName, kind: categoryKind, parentId: parentId || null }) }); const result = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível criar a categoria.'); setCategoryName(''); setParentId(''); await onChanged(); showToast('Categoria criada') } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') } finally { setSaving(false) } }
  const createCenter = async (event: React.FormEvent) => { event.preventDefault(); setSaving(true); try { const response = await apiFetch('/api/cost-centers', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: centerName, code: centerCode }) }); const result = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível criar o centro.'); setCenterName(''); setCenterCode(''); await onChanged(); showToast('Centro de custo criado') } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') } finally { setSaving(false) } }
  const toggle = async (resource: 'categories' | 'cost-centers', id: string, active: boolean) => { const response = await apiFetch(`/api/${resource}/${id}/active`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ active }) }); if (response.ok) { await onChanged(); showToast(active ? 'Cadastro ativado' : 'Cadastro arquivado') } else showToast('Não foi possível alterar o cadastro') }
  const orderedCategories = [...categories].sort((a, b) => Number(Boolean(a.parentId)) - Number(Boolean(b.parentId)) || a.name.localeCompare(b.name))

  return <div className="page structure-page"><section className="page-heading"><div><p className="eyebrow">ESTRUTURA GERENCIAL</p><h1>Categorias e centros de custo</h1><p>Organize receitas e despesas por finalidade, projeto ou departamento.</p></div></section>
    <div className="structure-tabs"><button className={tab === 'categories' ? 'active' : ''} onClick={() => setTab('categories')}><Tags size={16} /> Categorias</button><button className={tab === 'centers' ? 'active' : ''} onClick={() => setTab('centers')}><Landmark size={16} /> Centros de custo</button><button className={tab === 'allocations' ? 'active' : ''} onClick={() => setTab('allocations')}><PieChart size={16} /> Rateios</button><button className={tab === 'rules' ? 'active' : ''} onClick={() => setTab('rules')}><Sparkles size={16} /> Regras automáticas</button></div>
    {tab === 'categories' && <div className="structure-layout"><form className="structure-form panel" onSubmit={createCategory}><h3>Nova categoria</h3><p>Crie grupos e subcategorias para seus relatórios.</p><label>Nome<input value={categoryName} onChange={event => setCategoryName(event.target.value)} placeholder="Ex.: Marketing" /></label><label>Tipo<select value={categoryKind} onChange={event => { setCategoryKind(event.target.value as typeof categoryKind); setParentId('') }}><option value="Expense">Despesa</option><option value="Income">Receita</option><option value="Both">Entrada e saída</option></select></label><label>Categoria principal<select value={parentId} onChange={event => setParentId(event.target.value)}><option value="">Nenhuma — categoria raiz</option>{categories.filter(item => !item.parentId && item.isActive && (item.kind === categoryKind || item.kind === 'Both')).map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label><button className="primary-button" disabled={saving || !categoryName.trim()}><Plus size={15} /> Criar categoria</button></form><section className="structure-list panel"><div className="structure-list-head"><div><h3>Plano de categorias</h3><p>{categories.filter(item => item.isActive).length} categorias ativas</p></div></div>{orderedCategories.map(item => <div className={`structure-row ${item.parentId ? 'child' : ''} ${!item.isActive ? 'inactive' : ''}`} key={item.id}><span className="structure-icon"><Tags size={15} /></span><div><strong>{item.name}</strong><small>{item.parentName ? `${item.parentName} · ` : ''}{item.kind === 'Income' ? 'Receita' : item.kind === 'Expense' ? 'Despesa' : 'Entrada e saída'}</small></div><span className={`kind-pill ${item.kind.toLowerCase()}`}>{item.kind === 'Income' ? 'Receita' : item.kind === 'Expense' ? 'Despesa' : 'Ambos'}</span><button onClick={() => void toggle('categories', item.id, !item.isActive)}>{item.isActive ? 'Arquivar' : 'Ativar'}</button></div>)}</section></div>}
    {tab === 'centers' && <div className="structure-layout"><form className="structure-form panel" onSubmit={createCenter}><h3>Novo centro de custo</h3><p>Represente áreas, projetos ou unidades da empresa.</p><label>Nome<input value={centerName} onChange={event => setCenterName(event.target.value)} placeholder="Ex.: Projeto Atlas" /></label><label>Código<input value={centerCode} onChange={event => setCenterCode(event.target.value.toUpperCase())} placeholder="Ex.: ATL-01" maxLength={20} /></label><button className="primary-button" disabled={saving || !centerName.trim() || !centerCode.trim()}><Plus size={15} /> Criar centro</button></form><section className="structure-list panel"><div className="structure-list-head"><div><h3>Centros de custo</h3><p>{centers.filter(item => item.isActive).length} centros ativos</p></div></div>{centers.map(item => <div className={`structure-row ${!item.isActive ? 'inactive' : ''}`} key={item.id}><span className="center-code">{item.code}</span><div><strong>{item.name}</strong><small>{currency(item.allocatedAmount)} já rateados</small></div><span className={`active-pill ${item.isActive ? '' : 'off'}`}>{item.isActive ? 'Ativo' : 'Arquivado'}</span><button onClick={() => void toggle('cost-centers', item.id, !item.isActive)}>{item.isActive ? 'Arquivar' : 'Ativar'}</button></div>)}</section></div>}
    {tab === 'allocations' && <section className="allocation-panel panel"><div className="structure-list-head"><div><h3>Rateio de lançamentos</h3><p>Distribua cada valor entre projetos ou departamentos.</p></div></div>{transactions.filter(item => item.id).map(item => { const splits = allocations.filter(value => value.transactionId === item.id); return <div className="allocation-row" key={item.id}><div><strong>{item.name}</strong><small>{item.detail} · {item.category}</small></div><b className={item.value >= 0 ? 'positive-text' : 'negative-text'}>{currency(item.value)}</b><div className="allocation-tags">{splits.length ? splits.map(split => <span key={split.id}>{split.costCenterCode} {split.percentage.toLocaleString('pt-BR')}%</span>) : <span className="unallocated">Sem rateio</span>}</div><button className="secondary-button" onClick={() => setAllocating(item)}><PieChart size={14} /> Ratear</button></div>})}</section>}
    {tab === 'rules' && <CategorizationRulesPanel categories={categories} centers={centers} accounts={accounts} onTransactionsChanged={onChanged} showToast={showToast} />}
    {allocating?.id && <AllocationModal transaction={allocating} centers={centers.filter(item => item.isActive)} current={allocations.filter(item => item.transactionId === allocating.id)} onClose={() => setAllocating(null)} onSaved={async () => { setAllocating(null); await loadAllocations(); await onChanged(); showToast('Rateio salvo') }} />}
  </div>
}

function CategorizationRulesPanel({ categories, centers, accounts, onTransactionsChanged, showToast }: { categories: CategoryData[]; centers: CostCenterData[]; accounts: Account[]; onTransactionsChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [rules, setRules] = useState<CategorizationRuleData[]>([])
  const [editingId, setEditingId] = useState('')
  const [name, setName] = useState('')
  const [pattern, setPattern] = useState('')
  const [matchMode, setMatchMode] = useState<CategorizationRuleData['matchMode']>('Contains')
  const [transactionType, setTransactionType] = useState<'' | 'Income' | 'Expense'>('')
  const [accountId, setAccountId] = useState('')
  const [category, setCategory] = useState('')
  const [costCenterId, setCostCenterId] = useState('')
  const [priority, setPriority] = useState(100)
  const [saving, setSaving] = useState(false)
  const [preview, setPreview] = useState<{ rule: CategorizationRuleData; data: CategorizationPreviewData } | null>(null)

  const loadRules = async () => { const response = await apiFetch('/api/categorization-rules'); if (response.ok) setRules(await response.json() as CategorizationRuleData[]) }
  // As regras pertencem ao espaço selecionado e são recarregadas quando o painel é aberto.
  // oxlint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void loadRules() }, [])
  const reset = () => { setEditingId(''); setName(''); setPattern(''); setMatchMode('Contains'); setTransactionType(''); setAccountId(''); setCategory(''); setCostCenterId(''); setPriority(100) }
  const payload = (active = true) => ({ name, descriptionPattern: pattern, matchMode, transactionType: transactionType || null, accountId: accountId || null, category, costCenterId: costCenterId || null, priority, isActive: active })
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true)
    try {
      const response = await apiFetch(editingId ? `/api/categorization-rules/${editingId}` : '/api/categorization-rules', { method: editingId ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload(editingId ? rules.find(item => item.id === editingId)?.isActive ?? true : true)) })
      const result = await response.json().catch(() => null) as { detail?: string } | null
      if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível salvar a regra.')
      reset(); await loadRules(); showToast(editingId ? 'Regra atualizada' : 'Regra criada')
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setSaving(false) }
  }
  const edit = (rule: CategorizationRuleData) => { setEditingId(rule.id); setName(rule.name); setPattern(rule.descriptionPattern); setMatchMode(rule.matchMode); setTransactionType(rule.transactionType ?? ''); setAccountId(rule.accountId ?? ''); setCategory(rule.category); setCostCenterId(rule.costCenterId ?? ''); setPriority(rule.priority) }
  const updateRule = async (rule: CategorizationRuleData, changes: Partial<CategorizationRuleData>) => {
    const next = { ...rule, ...changes }
    const response = await apiFetch(`/api/categorization-rules/${rule.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: next.name, descriptionPattern: next.descriptionPattern, matchMode: next.matchMode, transactionType: next.transactionType ?? null, accountId: next.accountId ?? null, category: next.category, costCenterId: next.costCenterId ?? null, priority: next.priority, isActive: next.isActive }) })
    if (!response.ok) { showToast('Não foi possível alterar a regra'); return }
    await loadRules(); showToast(next.isActive ? 'Regra ativada' : 'Regra pausada')
  }
  const remove = async (rule: CategorizationRuleData) => {
    if (!window.confirm(`Excluir a regra “${rule.name}”?`)) return
    const response = await apiFetch(`/api/categorization-rules/${rule.id}`, { method: 'DELETE' })
    if (response.ok) { if (editingId === rule.id) reset(); await loadRules(); showToast('Regra excluída') } else showToast('Não foi possível excluir a regra')
  }
  const openPreview = async (rule: CategorizationRuleData) => {
    const response = await apiFetch(`/api/categorization-rules/${rule.id}/preview`)
    if (!response.ok) { showToast('Não foi possível calcular a prévia'); return }
    setPreview({ rule, data: await response.json() as CategorizationPreviewData })
  }
  const applyExisting = async () => {
    if (!preview) return; setSaving(true)
    try {
      const response = await apiFetch(`/api/categorization-rules/${preview.rule.id}/apply`, { method: 'POST' })
      const result = await response.json().catch(() => null) as { updated?: number; detail?: string } | null
      if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível aplicar a regra.')
      setPreview(null); await onTransactionsChanged(); showToast(`${result?.updated ?? 0} lançamento(s) atualizado(s)`)
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setSaving(false) }
  }
  const compatibleCategories = categories.filter(item => item.isActive && (!transactionType || item.kind === transactionType || item.kind === 'Both'))
  const modeLabel = { Contains: 'contém', StartsWith: 'começa com', Equals: 'é igual a' }

  return <div className="rule-layout"><form className="rule-form panel" onSubmit={submit}><div className="rule-form-head"><div><h3>{editingId ? 'Editar regra' : 'Nova regra automática'}</h3><p>A regra de maior prioridade compatível é aplicada primeiro.</p></div>{editingId && <button type="button" onClick={reset}><X size={15} /> Cancelar edição</button>}</div><label>Nome da regra<input value={name} onChange={event => setName(event.target.value)} placeholder="Ex.: Assinaturas Google" /></label><div className="rule-condition-grid"><label>Descrição<select value={matchMode} onChange={event => setMatchMode(event.target.value as typeof matchMode)}><option value="Contains">Contém</option><option value="StartsWith">Começa com</option><option value="Equals">É exatamente</option></select></label><label>Texto procurado<input value={pattern} onChange={event => setPattern(event.target.value)} placeholder="Ex.: GOOGLE" /></label></div><div className="rule-condition-grid"><label>Tipo<select value={transactionType} onChange={event => { setTransactionType(event.target.value as typeof transactionType); setCategory('') }}><option value="">Entradas e saídas</option><option value="Income">Somente entradas</option><option value="Expense">Somente saídas</option></select></label><label>Conta<select value={accountId} onChange={event => setAccountId(event.target.value)}><option value="">Todas as contas</option>{accounts.filter(item => item.isActive).map(item => <option value={item.id} key={item.id}>{item.name}</option>)}</select></label></div><div className="rule-action-box"><strong>Então aplicar</strong><label>Categoria<select value={category} onChange={event => setCategory(event.target.value)}><option value="">Selecione</option>{compatibleCategories.map(item => <option value={item.name} key={item.id}>{item.name}</option>)}</select></label><label>Centro de custo (opcional)<select value={costCenterId} onChange={event => setCostCenterId(event.target.value)}><option value="">Nenhum</option>{centers.filter(item => item.isActive).map(item => <option value={item.id} key={item.id}>{item.code} · {item.name}</option>)}</select></label></div><label>Prioridade<input type="number" min="1" max="1000" value={priority} onChange={event => setPriority(Number(event.target.value))} /><small>Quanto maior o número, mais cedo a regra será avaliada.</small></label><button className="primary-button" disabled={saving || !name.trim() || pattern.trim().length < 2 || !category}><Sparkles size={15} /> {saving ? 'Salvando…' : editingId ? 'Salvar alterações' : 'Criar regra'}</button></form><section className="rule-list panel"><div className="structure-list-head"><div><h3>Regras cadastradas</h3><p>{rules.filter(item => item.isActive).length} ativas · aplicadas por ordem de prioridade</p></div></div>{rules.map(rule => <article className={!rule.isActive ? 'inactive' : ''} key={rule.id}><span className="rule-priority">{rule.priority}</span><div className="rule-description"><strong>{rule.name}</strong><small>Descrição {modeLabel[rule.matchMode]} “{rule.descriptionPattern}”{rule.transactionType ? ` · ${rule.transactionType === 'Income' ? 'Entrada' : 'Saída'}` : ''}{rule.accountName ? ` · ${rule.accountName}` : ''}</small><p><ArrowRight size={12} /> {rule.category}{rule.costCenterName ? ` · ${rule.costCenterName}` : ''}</p></div><span className={`active-pill ${rule.isActive ? '' : 'off'}`}>{rule.isActive ? 'Ativa' : 'Pausada'}</span><div className="rule-actions"><button onClick={() => void openPreview(rule)}><Eye size={14} /> Prévia</button><button onClick={() => edit(rule)}><Pencil size={14} /> Editar</button><button onClick={() => void updateRule(rule, { isActive: !rule.isActive })}>{rule.isActive ? <Ban size={14} /> : <CheckCircle2 size={14} />}{rule.isActive ? 'Pausar' : 'Ativar'}</button><button className="danger-link" onClick={() => void remove(rule)} aria-label={`Excluir ${rule.name}`}><X size={14} /></button></div></article>)}{rules.length === 0 && <div className="rule-empty"><Sparkles size={25} /><strong>Nenhuma regra criada</strong><span>Automatize descrições recorrentes para economizar tempo.</span></div>}</section>{preview && <div className="modal-backdrop" onMouseDown={() => setPreview(null)}><section className="entry-modal rule-preview-modal" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>APLICAÇÃO RETROATIVA</p><h2>Prévia de “{preview.rule.name}”</h2></div><button onClick={() => setPreview(null)}><X size={20} /></button></div><div className="preview-summary"><Sparkles size={18} /><div><strong>{preview.data.matchCount} lançamento(s) encontrado(s)</strong><span>Nenhuma alteração foi feita ainda.</span></div></div><div className="preview-transactions">{preview.data.samples.map(item => <article key={item.id}><div><strong>{item.description}</strong><small>{item.accountName} · categoria atual: {item.category}</small></div><b>{currency(item.signedAmount)}</b></article>)}{preview.data.matchCount === 0 && <p>Nenhum lançamento atual corresponde às condições.</p>}</div><p className="preview-warning">Ao confirmar, a categoria será alterada e o centro de custo atual será substituído quando a regra definir um.</p><div className="modal-actions"><button className="secondary-button" onClick={() => setPreview(null)}>Cancelar</button><button className="primary-button" disabled={saving || preview.data.matchCount === 0} onClick={() => void applyExisting()}>{saving ? 'Aplicando…' : `Aplicar a ${preview.data.matchCount} lançamento(s)`}</button></div></section></div>}</div>
}

function AllocationModal({ transaction, centers, current, onClose, onSaved }: { transaction: Transaction; centers: CostCenterData[]; current: AllocationData[]; onClose: () => void; onSaved: () => Promise<void> }) {
  const total = Math.abs(transaction.value); const [rows, setRows] = useState<{ costCenterId: string; amount: string }[]>(current.length ? current.map(item => ({ costCenterId: item.costCenterId, amount: item.amount.toFixed(2).replace('.', ',') })) : [{ costCenterId: centers[0]?.id ?? '', amount: total.toFixed(2).replace('.', ',') }]); const [error, setError] = useState(''); const [saving, setSaving] = useState(false)
  const parsed = rows.reduce((sum, row) => sum + (Number(row.amount.replace(/\./g, '').replace(',', '.')) || 0), 0); const remaining = total - parsed
  const update = (index: number, field: 'costCenterId' | 'amount', value: string) => setRows(items => items.map((item, itemIndex) => itemIndex === index ? { ...item, [field]: value } : item))
  const submit = async (event: React.FormEvent) => { event.preventDefault(); if (!transaction.id) return; setSaving(true); setError(''); try { const body = rows.map(row => ({ costCenterId: row.costCenterId, amount: Number(row.amount.replace(/\./g, '').replace(',', '.')) })); const response = await apiFetch(`/api/transactions/${transaction.id}/allocations`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); const result = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível salvar o rateio.'); await onSaved() } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setSaving(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal allocation-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>Centros de custo</p><h2>Ratear lançamento</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div><div className="allocation-summary"><span>{transaction.name}</span><strong>{currency(total)}</strong></div><div className="allocation-fields">{rows.map((row, index) => <div className="allocation-field" key={index}><select value={row.costCenterId} onChange={event => update(index, 'costCenterId', event.target.value)}>{centers.map(center => <option value={center.id} key={center.id}>{center.code} · {center.name}</option>)}</select><input value={row.amount} onChange={event => update(index, 'amount', event.target.value)} inputMode="decimal" /><button type="button" onClick={() => setRows(items => items.filter((_, itemIndex) => itemIndex !== index))} aria-label="Remover rateio"><X size={15} /></button></div>)}</div><button type="button" className="add-allocation" onClick={() => setRows(items => [...items, { costCenterId: centers.find(center => !items.some(item => item.costCenterId === center.id))?.id ?? centers[0]?.id ?? '', amount: '' }])}><Plus size={14} /> Adicionar centro</button><div className={`allocation-balance ${Math.abs(remaining) < .01 ? 'ok' : ''}`}><span>Total distribuído: {currency(parsed)}</span><strong>{Math.abs(remaining) < .01 ? 'Rateio completo' : `Faltam ${currency(remaining)}`}</strong></div>{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving || Math.abs(remaining) >= .01 || rows.length === 0}>{saving ? 'Salvando…' : 'Salvar rateio'}</button></div></form></div>
}

function ReconciliationPage({ accounts, categories, centers, onChanged, showToast }: { accounts: Account[]; categories: CategoryData[]; centers: CostCenterData[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [accountId, setAccountId] = useState('all')
  const [filter, setFilter] = useState<'Pending' | 'Reconciled' | 'Ignored' | 'History'>('Pending')
  const [issueFilter, setIssueFilter] = useState<'All' | 'NoMatch' | 'LowConfidence' | 'NoCategory' | 'PotentialDuplicate' | 'HighConfidence'>('All')
  const [pendingQuery, setPendingQuery] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const [minimumAmount, setMinimumAmount] = useState('')
  const [maximumAmount, setMaximumAmount] = useState('')
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [bulkCategory, setBulkCategory] = useState('')
  const [data, setData] = useState<ReconciliationData | null>(null)
  const [history, setHistory] = useState<ReconciliationHistoryData[]>([])
  const [historyQuery, setHistoryQuery] = useState('')
  const [historyAction, setHistoryAction] = useState('All')
  const [undoEntry, setUndoEntry] = useState<BankEntry | null>(null)
  const [loading, setLoading] = useState(false)
  const [busyId, setBusyId] = useState('')
  const [previewFile, setPreviewFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<BankImportPreviewData | null>(null)
  const [decisions, setDecisions] = useState<Record<number, BankImportDecision>>({})

  const load = useCallback(async (selectedAccount: string) => {
    setLoading(true)
    try { const response = await apiFetch(selectedAccount === 'all' ? '/api/reconciliation' : `/api/reconciliation?accountId=${selectedAccount}`); if (!response.ok) throw new Error(); setData(await response.json() as ReconciliationData) }
    catch { showToast('Não foi possível carregar a conciliação') }
    finally { setLoading(false) }
  }, [showToast])

  const loadHistory = useCallback(async (selectedAccount: string) => {
    try { const response = await apiFetch(selectedAccount === 'all' ? '/api/reconciliation/history' : `/api/reconciliation/history?accountId=${selectedAccount}`); if (!response.ok) throw new Error(); setHistory(await response.json() as ReconciliationHistoryData[]) }
    catch { showToast('Não foi possível carregar o histórico da conciliação') }
  }, [showToast])

  /* oxlint-disable react/set-state-in-effect */
  useEffect(() => { void load(accountId); void loadHistory(accountId) }, [accountId, load, loadHistory])
  /* oxlint-enable react/set-state-in-effect */

  const previewImport = async (file?: File) => {
    if (!file || !accountId) return
    if (file.size > 10_000_000) { showToast('O arquivo deve ter no máximo 10 MB'); return }
    const body = new FormData(); body.append('accountId', accountId); body.append('file', file)
    setBusyId('preview')
    try {
      const response = await apiFetch('/api/reconciliation/import/preview', { method: 'POST', body })
      const result = await response.json() as BankImportPreviewData & { detail?: string }
      if (!response.ok) throw new Error(result.detail ?? 'Falha ao analisar o extrato.')
      const initialDecisions = Object.fromEntries(result.rows.filter(row => row.status === 'Ready').map(row => {
        const action: BankImportDecision['action'] = row.matchSuggestion?.confidence === 'High' ? 'Match' : row.categorySuggestion ? 'Create' : 'Pending'
        return [row.rowNumber, { rowNumber: row.rowNumber, action, transactionId: action === 'Match' ? row.matchSuggestion?.transactionId : undefined, category: row.categorySuggestion?.category, costCenterId: row.categorySuggestion?.costCenterId } satisfies BankImportDecision]
      }))
      setDecisions(initialDecisions); setPreviewFile(file); setPreview(result)
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Falha ao analisar o extrato') }
    finally { setBusyId('') }
  }

  const confirmImport = async () => {
    if (!previewFile || !preview || !accountId || preview.ready === 0) return
    const missingCategory = Object.values(decisions).find(decision => decision.action === 'Create' && !decision.category)
    if (missingCategory) { showToast(`Selecione uma categoria para a linha ${missingCategory.rowNumber}`); return }
    const body = new FormData(); body.append('accountId', accountId); body.append('file', previewFile); body.append('decisions', JSON.stringify(Object.values(decisions)))
    setBusyId('import')
    try {
      const response = await apiFetch('/api/reconciliation/import/confirm', { method: 'POST', body })
      const result = await response.json() as { imported?: number; matched?: number; created?: number; ignored?: number; pending?: number; duplicates?: number; invalid?: number; rulesCreated?: number; detail?: string }
      if (!response.ok) throw new Error(result.detail ?? 'Falha ao importar o extrato.')
      setPreview(null); setPreviewFile(null); setDecisions({}); setFilter('Pending')
      await load(accountId); await loadHistory(accountId); await onChanged(); showToast(`${result.imported ?? 0} importadas · ${result.matched ?? 0} conciliadas · ${result.created ?? 0} criadas${result.rulesCreated ? ` · ${result.rulesCreated} nova(s) regra(s)` : ''}`)
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Falha ao importar') }
    finally { setBusyId('') }
  }

  const postAction = async (entry: BankEntry, action: 'match' | 'create' | 'ignore' | 'reopen') => {
    let payload: object | undefined
    if (action === 'match') { if (!entry.suggestion) return; payload = { transactionId: entry.suggestion.transactionId } }
    if (action === 'create') { const category = window.prompt('Categoria do novo lançamento:', entry.categorySuggestion?.category ?? (entry.type === 'Income' ? 'Receita bancária' : 'Despesa bancária')); if (!category) return; payload = { category } }
    setBusyId(entry.id)
    try {
      const response = await apiFetch(`/api/reconciliation/${entry.id}/${action}`, { method: 'POST', headers: payload ? { 'Content-Type': 'application/json' } : undefined, body: payload ? JSON.stringify(payload) : undefined })
      const problem = await response.json().catch(() => null) as { detail?: string } | null
      if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível concluir a ação.')
      setSelectedIds(current => { const next = new Set(current); next.delete(entry.id); return next })
      await load(accountId); await loadHistory(accountId); await onChanged(); showToast(action === 'ignore' ? 'Movimentação ignorada' : action === 'reopen' ? 'Movimentação reaberta' : 'Conciliação concluída')
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setBusyId('') }
  }

  const statusEntries = data?.entries.filter(item => item.status === filter) ?? []
  const normalizedQuery = pendingQuery.trim().toLocaleLowerCase('pt-BR')
  const isPotentialDuplicate = (entry: BankEntry) => (data?.entries ?? []).some(other => other.id !== entry.id && other.status === 'Pending' && other.accountId === entry.accountId && other.signedAmount === entry.signedAmount && other.description.trim().toLocaleLowerCase('pt-BR') === entry.description.trim().toLocaleLowerCase('pt-BR') && Math.abs(new Date(`${other.occurredOn}T12:00:00`).getTime() - new Date(`${entry.occurredOn}T12:00:00`).getTime()) <= 86_400_000)
  const entries = statusEntries.filter(item => {
    if (normalizedQuery && !`${item.description} ${item.accountName}`.toLocaleLowerCase('pt-BR').includes(normalizedQuery)) return false
    if (dateFrom && item.occurredOn < dateFrom || dateTo && item.occurredOn > dateTo) return false
    const minimum = Number(minimumAmount.replace(',', '.')); const maximum = Number(maximumAmount.replace(',', '.'))
    if (minimumAmount && Number.isFinite(minimum) && item.amount < minimum || maximumAmount && Number.isFinite(maximum) && item.amount > maximum) return false
    if (filter !== 'Pending' || issueFilter === 'All') return true
    if (issueFilter === 'NoMatch') return !item.suggestion
    if (issueFilter === 'LowConfidence') return !!item.suggestion && item.suggestion.confidence !== 'High'
    if (issueFilter === 'NoCategory') return !item.suggestion && !item.categorySuggestion
    if (issueFilter === 'PotentialDuplicate') return isPotentialDuplicate(item)
    return item.suggestion?.confidence === 'High'
  })
  const selectedEntries = (data?.entries ?? []).filter(item => selectedIds.has(item.id) && item.status === 'Pending')
  const selectedTypes = new Set(selectedEntries.map(item => item.type))
  const bulkCategoryOptions = categories.filter(category => category.isActive && (category.kind === 'Both' || selectedTypes.size === 1 && selectedTypes.has(category.kind)))
  const toggleSelected = (id: string) => setSelectedIds(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next })
  const toggleAllVisible = () => setSelectedIds(current => { const next = new Set(current); const selectable = entries.filter(item => item.status === 'Pending'); const allSelected = selectable.length > 0 && selectable.every(item => next.has(item.id)); selectable.forEach(item => allSelected ? next.delete(item.id) : next.add(item.id)); return next })
  const runBulk = async (action: 'match' | 'create' | 'ignore') => {
    let targets = selectedEntries
    if (action === 'match') targets = targets.filter(item => item.suggestion)
    if (!targets.length) { showToast(action === 'match' ? 'Selecione pendências com correspondência sugerida' : 'Selecione ao menos uma pendência'); return }
    if (action === 'create' && !bulkCategory && targets.some(item => !item.categorySuggestion)) { showToast('Selecione uma categoria para os itens sem regra'); return }
    setBusyId('bulk')
    const results = await Promise.all(targets.map(async entry => {
      try {
        const payload = action === 'match' ? { transactionId: entry.suggestion!.transactionId } : action === 'create' ? { category: bulkCategory || entry.categorySuggestion?.category || '' } : undefined
        const response = await apiFetch(`/api/reconciliation/${entry.id}/${action}`, { method: 'POST', headers: payload ? { 'Content-Type': 'application/json' } : undefined, body: payload ? JSON.stringify(payload) : undefined })
        return response.ok
      } catch { return false }
    }))
    const completed = results.filter(Boolean).length
    setSelectedIds(new Set()); setBusyId(''); await load(accountId); await loadHistory(accountId); await onChanged()
    showToast(results.every(Boolean) ? `${completed} pendência(s) processada(s)` : `${completed} concluída(s) · ${results.length - completed} não processada(s)`)
  }
  const confirmUndo = async (removeCreatedTransaction: boolean) => {
    if (!undoEntry) return
    setBusyId(undoEntry.id)
    try {
      const response = await apiFetch(`/api/reconciliation/${undoEntry.id}/undo`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ removeCreatedTransaction }) })
      const problem = await response.json().catch(() => null) as { detail?: string } | null
      if (!response.ok) throw new Error(problem?.detail ?? 'Não foi possível desfazer a conciliação.')
      setUndoEntry(null); await load(accountId); await loadHistory(accountId); await onChanged(); showToast(removeCreatedTransaction ? 'Conciliação desfeita e movimentação estornada' : 'Conciliação desfeita; movimentação mantida')
    } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') }
    finally { setBusyId('') }
  }
  const historyLabels: Record<string, string> = { ImportedPending: 'Importado para revisão', Matched: 'Conciliado com existente', Created: 'Movimentação criada', Ignored: 'Ignorado', Reopened: 'Reaberto', UndoneKeptTransaction: 'Desfeito · movimentação mantida', UndoneAndRemoved: 'Desfeito · movimentação estornada' }
  const filteredHistory = history.filter(item => (historyAction === 'All' || item.action === historyAction) && (!historyQuery.trim() || `${item.description} ${item.accountName} ${item.userName} ${item.category ?? ''}`.toLocaleLowerCase('pt-BR').includes(historyQuery.trim().toLocaleLowerCase('pt-BR'))))
  return <div className="page reconciliation-page">
    <section className="page-heading reconciliation-heading"><div><p className="eyebrow">CENTRAL DE PENDÊNCIAS</p><h1>Conciliação bancária</h1><p>Revise divergências de todas as contas e processe vários lançamentos de uma vez.</p></div><div className="reconciliation-controls"><select value={accountId} onChange={event => { setAccountId(event.target.value); setSelectedIds(new Set()); setPreview(null); setPreviewFile(null); setDecisions({}) }} aria-label="Conta bancária"><option value="all">Todas as contas</option>{accounts.map(account => <option value={account.id} key={account.id}>{account.name} · {account.institution}</option>)}</select><label className={`primary-button upload-button ${busyId === 'preview' ? 'is-busy' : ''}`} title={accountId === 'all' ? 'Selecione uma conta específica para importar' : undefined}><FileUp size={17} /> {busyId === 'preview' ? 'Analisando…' : 'Selecionar OFX ou CSV'}<input type="file" accept=".ofx,.csv,text/csv,application/x-ofx" disabled={accountId === 'all' || busyId === 'preview'} onChange={event => { void previewImport(event.target.files?.[0]); event.target.value = '' }} /></label></div></section>
    <section className="reconciliation-kpis"><article><small>Pendentes</small><strong>{data?.pending ?? 0}</strong><span>Precisam de revisão</span></article><article><small>Conciliadas</small><strong>{data?.reconciled ?? 0}</strong><span>Vinculadas ao sistema</span></article><article><small>Saldo pelo extrato</small><strong>{currency(data?.importedBalance ?? 0)}</strong><span>Saldo inicial + importações</span></article><article className={(data?.difference ?? 0) === 0 ? 'balanced' : 'unbalanced'}><small>Diferença</small><strong>{currency(data?.difference ?? 0)}</strong><span>{(data?.difference ?? 0) === 0 ? 'Conta conciliada' : `Sistema: ${currency(data?.systemBalance ?? 0)}`}</span></article></section>
    {filter !== 'History' && <section className="pending-filters"><div className="pending-search"><Search size={15} /><input value={pendingQuery} onChange={event => setPendingQuery(event.target.value)} placeholder="Buscar descrição ou conta" /></div><select value={issueFilter} onChange={event => setIssueFilter(event.target.value as typeof issueFilter)} disabled={filter !== 'Pending'}><option value="All">Todos os motivos</option><option value="NoMatch">Sem correspondência</option><option value="LowConfidence">Confiança média ou baixa</option><option value="NoCategory">Sem categoria sugerida</option><option value="PotentialDuplicate">Possíveis duplicidades</option><option value="HighConfidence">Alta confiança</option></select><label>De<input type="date" value={dateFrom} onChange={event => setDateFrom(event.target.value)} /></label><label>Até<input type="date" value={dateTo} onChange={event => setDateTo(event.target.value)} /></label><label>Valor mín.<input inputMode="decimal" value={minimumAmount} onChange={event => setMinimumAmount(event.target.value)} placeholder="0,00" /></label><label>Valor máx.<input inputMode="decimal" value={maximumAmount} onChange={event => setMaximumAmount(event.target.value)} placeholder="0,00" /></label></section>}
    {filter === 'History' && <section className="history-filters"><div className="pending-search"><Search size={15} /><input value={historyQuery} onChange={event => setHistoryQuery(event.target.value)} placeholder="Buscar lançamento, conta ou usuário" /></div><select value={historyAction} onChange={event => setHistoryAction(event.target.value)}><option value="All">Todas as ações</option>{Object.entries(historyLabels).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select><span>{filteredHistory.length} evento(s)</span></section>}
    {filter === 'Pending' && <section className={`pending-bulk-bar ${selectedIds.size ? 'visible' : ''}`}><label><input type="checkbox" checked={entries.filter(item => item.status === 'Pending').length > 0 && entries.filter(item => item.status === 'Pending').every(item => selectedIds.has(item.id))} onChange={toggleAllVisible} /> Selecionar visíveis</label><strong>{selectedIds.size} selecionada(s)</strong><select value={bulkCategory} onChange={event => setBulkCategory(event.target.value)}><option value="">Categoria para itens sem regra</option>{bulkCategoryOptions.map(category => <option value={category.name} key={category.id}>{category.name}</option>)}</select><button onClick={() => void runBulk('match')} disabled={busyId === 'bulk'}><Link2 size={14} /> Conciliar sugestões</button><button onClick={() => void runBulk('create')} disabled={busyId === 'bulk'}><Plus size={14} /> Criar movimentações</button><button className="bulk-ignore" onClick={() => void runBulk('ignore')} disabled={busyId === 'bulk'}><Ban size={14} /> Ignorar</button></section>}
    <section className="reconciliation-panel"><div className="reconciliation-tabs">{([['Pending', 'Para revisar', data?.pending], ['Reconciled', 'Conciliadas', data?.reconciled], ['Ignored', 'Ignoradas', data?.ignored], ['History', 'Histórico', history.length]] as const).map(([id, label, count]) => <button className={filter === id ? 'active' : ''} onClick={() => setFilter(id)} key={id}>{label}<em>{count ?? 0}</em></button>)}</div>
      {filter !== 'History' && <div className="bank-list">{loading ? <div className="empty-cashflow"><Clock3 size={24} /><strong>Carregando extrato…</strong></div> : entries.map(entry => <BankEntryRow entry={entry} potentialDuplicate={isPotentialDuplicate(entry)} busy={busyId === entry.id || busyId === 'bulk'} selected={selectedIds.has(entry.id)} toggleSelected={() => toggleSelected(entry.id)} undo={() => setUndoEntry(entry)} action={action => void postAction(entry, action)} key={entry.id} />)}{!loading && entries.length === 0 && <div className="reconciliation-empty"><Landmark size={28} /><strong>{filter === 'Pending' ? 'Nenhuma pendência neste filtro' : 'Nenhum registro nesta etapa'}</strong><span>{filter === 'Pending' ? 'Ajuste os filtros ou importe um novo extrato para continuar.' : 'Os registros aparecerão aqui conforme forem revisados.'}</span></div>}</div>}
      {filter === 'History' && <div className="reconciliation-history">{filteredHistory.map(item => <article key={item.id}><span className={`history-mark ${item.action.toLowerCase()}`}><History size={15} /></span><div><header><strong>{historyLabels[item.action] ?? item.action}</strong><time>{new Date(item.occurredAt).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}</time></header><h3>{item.description}</h3><p>{item.accountName} · por {item.userName}</p><div className="history-details">{item.category && <span><Tags size={12} /> {item.category}</span>}{item.costCenterName && <span><Building2 size={12} /> {item.costCenterName}</span>}{item.ruleName && <span><Sparkles size={12} /> {item.ruleName}</span>}{item.transactionId && <span><Link2 size={12} /> {item.transactionWasCreated ? 'Movimentação criada' : 'Movimentação vinculada'}{item.transactionRemoved ? ' e estornada' : ''}</span>}</div>{item.details && <small>{item.details}</small>}</div></article>)}{filteredHistory.length === 0 && <div className="reconciliation-empty"><History size={28} /><strong>Nenhum evento encontrado</strong><span>As próximas decisões de conciliação aparecerão nesta linha do tempo.</span></div>}</div>}
    </section>
    {preview && previewFile && <BankImportPreviewModal preview={preview} account={accounts.find(account => account.id === accountId)} categories={categories} centers={centers} decisions={decisions} onDecision={(rowNumber, decision) => setDecisions(current => ({ ...current, [rowNumber]: { ...current[rowNumber], ...decision, rowNumber } }))} busy={busyId === 'import'} onClose={() => { if (busyId !== 'import') { setPreview(null); setPreviewFile(null); setDecisions({}) } }} onConfirm={() => void confirmImport()} />}
    {undoEntry && <UndoReconciliationModal entry={undoEntry} busy={busyId === undoEntry.id} onClose={() => { if (busyId !== undoEntry.id) setUndoEntry(null) }} onConfirm={remove => void confirmUndo(remove)} />}
  </div>
}

function BankImportPreviewModal({ preview, account, categories, centers, decisions, onDecision, busy, onClose, onConfirm }: { preview: BankImportPreviewData; account?: Account; categories: CategoryData[]; centers: CostCenterData[]; decisions: Record<number, BankImportDecision>; onDecision: (rowNumber: number, decision: Partial<BankImportDecision>) => void; busy: boolean; onClose: () => void; onConfirm: () => void }) {
  const [filter, setFilter] = useState<'All' | 'Ready' | 'Issues'>('All')
  const rows = preview.rows.filter(row => filter === 'All' || (filter === 'Ready' ? row.status === 'Ready' : row.status !== 'Ready'))
  const statusLabel = (status: BankImportPreviewRow['status']) => status === 'Ready' ? 'Pronto' : status === 'Duplicate' ? 'Duplicado' : 'Inválido'
  const confidenceLabel = (confidence: BankImportMatchSuggestion['confidence']) => confidence === 'High' ? 'Alta confiança' : confidence === 'Medium' ? 'Média confiança' : 'Baixa confiança'
  const setAction = (row: BankImportPreviewRow, action: BankImportDecision['action']) => onDecision(row.rowNumber, { action, transactionId: action === 'Match' ? row.matchSuggestion?.transactionId : undefined, category: action === 'Create' ? decisions[row.rowNumber]?.category ?? row.categorySuggestion?.category : undefined, costCenterId: action === 'Create' ? decisions[row.rowNumber]?.costCenterId ?? row.categorySuggestion?.costCenterId : undefined })
  return <div className="modal-backdrop" onMouseDown={onClose}><section className="entry-modal import-preview-modal" role="dialog" aria-modal="true" aria-labelledby="import-preview-title" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>PRÉ-VISUALIZAÇÃO DO EXTRATO</p><h2 id="import-preview-title">Revise antes de importar</h2><span>{preview.fileName} · {preview.format} · {account?.name}</span></div><button onClick={onClose} disabled={busy} aria-label="Fechar"><X size={20} /></button></div>
    <div className="import-preview-summary"><article className="ready"><CheckCircle2 size={17} /><div><strong>{preview.ready}</strong><span>Prontos</span></div></article><article className="duplicate"><RotateCcw size={17} /><div><strong>{preview.duplicates}</strong><span>Duplicados</span></div></article><article className="invalid"><AlertTriangle size={17} /><div><strong>{preview.invalid}</strong><span>Inválidos</span></div></article><article><ReceiptText size={17} /><div><strong>{preview.total}</strong><span>Total de linhas</span></div></article></div>
    <div className="import-preview-balance"><span>Entradas válidas <strong className="positive-text">+ {currency(preview.totalIncome)}</strong></span><span>Saídas válidas <strong className="negative-text">− {currency(preview.totalExpense)}</strong></span><span>Movimento líquido <strong>{currency(preview.totalIncome - preview.totalExpense)}</strong></span></div>
    <div className="import-preview-tabs">{([['All', 'Todos', preview.total], ['Ready', 'Prontos', preview.ready], ['Issues', 'Com problemas', preview.duplicates + preview.invalid]] as const).map(([id, label, count]) => <button className={filter === id ? 'active' : ''} onClick={() => setFilter(id)} key={id}>{label}<em>{count}</em></button>)}</div>
    <div className="import-preview-table"><div className="import-preview-columns"><span>Linha</span><span>Lançamento e sugestões</span><span>Data</span><span>Valor</span><span>Decisão</span></div>{rows.map(row => { const decision = decisions[row.rowNumber]; const compatibleCategories = categories.filter(category => category.isActive && (category.kind === 'Both' || category.kind === ((row.signedAmount ?? 0) >= 0 ? 'Income' : 'Expense'))); return <article className={row.status.toLowerCase()} key={`${row.rowNumber}-${row.externalId ?? row.description}`}><span>#{row.rowNumber}</span><div className="import-row-description"><strong>{row.description || 'Sem descrição'}</strong>{row.issue && <small>{row.issue}</small>}{row.matchSuggestion && <span className={`import-match ${row.matchSuggestion.confidence.toLowerCase()}`}><Sparkles size={12} /><span><b>{confidenceLabel(row.matchSuggestion.confidence)} · {row.matchSuggestion.score}%</b>{row.matchSuggestion.description} · {row.matchSuggestion.category} · {row.matchSuggestion.dateDistanceDays} dia(s)</span></span>}{row.categorySuggestion && <span className="import-category-suggestion"><Tags size={12} /><span><b>Regra: {row.categorySuggestion.ruleName}</b>{row.categorySuggestion.category}{row.categorySuggestion.costCenterName ? ` · ${row.categorySuggestion.costCenterName}` : ''}</span></span>}</div><span>{row.occurredOn ? new Date(`${row.occurredOn}T12:00:00`).toLocaleDateString('pt-BR') : '—'}</span><b className={(row.signedAmount ?? 0) >= 0 ? 'positive-text' : 'negative-text'}>{row.signedAmount == null ? '—' : currency(row.signedAmount)}</b>{row.status === 'Ready' ? <select className={`import-action ${decision?.action?.toLowerCase() ?? 'pending'}`} value={decision?.action ?? 'Pending'} onChange={event => setAction(row, event.target.value as BankImportDecision['action'])} aria-label={`Decisão da linha ${row.rowNumber}`}><option value="Pending">Revisar depois</option>{row.matchSuggestion && <option value="Match">Conciliar existente</option>}<option value="Create">Criar movimentação</option><option value="Ignore">Ignorar</option></select> : <em>{statusLabel(row.status)}</em>}{row.status === 'Ready' && decision?.action === 'Create' && <div className="import-row-editor"><label>Categoria<select value={decision.category ?? ''} onChange={event => onDecision(row.rowNumber, { category: event.target.value })}><option value="">Selecione uma categoria</option>{compatibleCategories.map(category => <option value={category.name} key={category.id}>{category.name}</option>)}</select></label><label>Centro de custo<select value={decision.costCenterId ?? ''} onChange={event => onDecision(row.rowNumber, { costCenterId: event.target.value || undefined })}><option value="">Sem centro de custo</option>{centers.filter(center => center.isActive).map(center => <option value={center.id} key={center.id}>{center.code} · {center.name}</option>)}</select></label><span>{row.categorySuggestion ? <><Sparkles size={13} /> Sugestão preenchida pela regra</> : <><AlertTriangle size={13} /> Defina a categoria antes de importar</>}</span><label className="import-save-rule"><input type="checkbox" checked={decision.saveAsRule ?? false} onChange={event => onDecision(row.rowNumber, { saveAsRule: event.target.checked, rulePattern: decision.rulePattern ?? row.description.slice(0, 120) })} /> <i /> Criar regra para lançamentos semelhantes</label>{decision.saveAsRule && <label className="import-rule-pattern">Texto que a descrição deve conter<input value={decision.rulePattern ?? row.description.slice(0, 120)} maxLength={120} onChange={event => onDecision(row.rowNumber, { rulePattern: event.target.value })} /></label>}</div>}{row.status === 'Ready' && decision?.action === 'Match' && row.matchSuggestion && <div className="import-row-editor match-review"><span><Link2 size={13} /> Será conciliado com <strong>{row.matchSuggestion.description}</strong>, de {new Date(`${row.matchSuggestion.occurredOn}T12:00:00`).toLocaleDateString('pt-BR')}.</span></div>}</article> })}{rows.length === 0 && <div className="import-preview-empty">Nenhum lançamento neste filtro.</div>}</div>
    {(preview.duplicates > 0 || preview.invalid > 0) && <p className="import-preview-note"><AlertTriangle size={15} /> Duplicados e inválidos não serão gravados. Confira os detalhes acima antes de continuar.</p>}
    <div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose} disabled={busy}>Cancelar</button><button type="button" className="primary-button" onClick={onConfirm} disabled={busy || preview.ready === 0}>{busy ? 'Importando…' : preview.ready > 0 ? `Confirmar ${preview.ready} lançamento${preview.ready === 1 ? '' : 's'}` : 'Nada para importar'}</button></div>
  </section></div>
}

function UndoReconciliationModal({ entry, busy, onClose, onConfirm }: { entry: BankEntry; busy: boolean; onClose: () => void; onConfirm: (removeCreatedTransaction: boolean) => void }) {
  return <div className="modal-backdrop" onMouseDown={onClose}><section className="entry-modal undo-reconciliation-modal" role="dialog" aria-modal="true" aria-labelledby="undo-title" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>REVER DECISÃO</p><h2 id="undo-title">Desfazer conciliação</h2></div><button onClick={onClose} disabled={busy} aria-label="Fechar"><X size={20} /></button></div><div className="undo-entry-summary"><RotateCcw size={20} /><div><strong>{entry.description}</strong><span>{entry.accountName} · {currency(entry.amount)}</span></div></div><p>A movimentação bancária voltará para a Central de Pendências. Esta operação será registrada no histórico.</p><div className="undo-options"><button onClick={() => onConfirm(false)} disabled={busy}><span><Link2 size={17} /></span><div><strong>Manter movimentação financeira</strong><small>Remove apenas o vínculo da conciliação. O lançamento continuará nos relatórios e no saldo.</small></div></button>{entry.transactionWasCreated && <button className="danger" onClick={() => onConfirm(true)} disabled={busy}><span><Ban size={17} /></span><div><strong>Estornar movimentação criada</strong><small>Remove o efeito financeiro da movimentação automática e preserva seu registro de auditoria.</small></div></button>}</div><div className="modal-actions"><button className="secondary-button" onClick={onClose} disabled={busy}>Cancelar</button></div></section></div>
}

function BankEntryRow({ entry, potentialDuplicate, busy, selected, toggleSelected, undo, action }: { entry: BankEntry; potentialDuplicate: boolean; busy: boolean; selected: boolean; toggleSelected: () => void; undo: () => void; action: (value: 'match' | 'create' | 'ignore' | 'reopen') => void }) {
  const date = new Date(`${entry.occurredOn}T12:00:00`).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })
  const confidenceLabel = entry.suggestion?.confidence === 'High' ? 'Alta confiança' : entry.suggestion?.confidence === 'Medium' ? 'Média confiança' : 'Baixa confiança'
  return <article className={`bank-entry ${entry.status.toLowerCase()} ${selected ? 'selected' : ''}`}><div className="bank-entry-main">{entry.status === 'Pending' && <label className="pending-check"><input type="checkbox" checked={selected} onChange={toggleSelected} aria-label={`Selecionar ${entry.description}`} /></label>}<span className={entry.type === 'Income' ? 'income' : 'expense'}>{entry.type === 'Income' ? <ArrowDownLeft size={18} /> : <ArrowUpRight size={18} />}</span><div><strong>{entry.description}</strong><small>{date} · {entry.accountName}</small>{potentialDuplicate && <em className="pending-duplicate">Possível duplicidade</em>}</div><b className={entry.type === 'Income' ? 'positive-text' : 'negative-text'}>{entry.type === 'Income' ? '+' : '−'} {currency(entry.amount)}</b></div>{entry.status === 'Pending' && <div className="match-area">{entry.suggestion ? <div className={`match-suggestion ${entry.suggestion.confidence.toLowerCase()}`}><Sparkles size={16} /><div><small>{confidenceLabel} · {entry.suggestion.score}%</small><strong>{entry.suggestion.description}</strong><span>{entry.suggestion.category} · diferença de {entry.suggestion.dateDistanceDays} dia(s)</span></div><button disabled={busy} onClick={() => action('match')}><Link2 size={14} /> Conciliar</button></div> : <div className="no-match"><span>{entry.categorySuggestion ? `Regra encontrada: ${entry.categorySuggestion.category}${entry.categorySuggestion.costCenterName ? ` · ${entry.categorySuggestion.costCenterName}` : ''}` : 'Sem correspondência ou categoria sugerida.'}</span><button disabled={busy} onClick={() => action('create')}><Plus size={14} /> Criar lançamento</button></div>}<button className="ignore-action" disabled={busy} onClick={() => action('ignore')}>Ignorar</button></div>}{entry.status !== 'Pending' && <div className="reviewed-entry"><span className={`reviewed-status ${entry.status.toLowerCase()}`}><CheckCircle2 size={14} /> {entry.status === 'Reconciled' ? 'Conciliada' : 'Ignorada'}</span><button disabled={busy} onClick={entry.status === 'Reconciled' ? undo : () => action('reopen')}><RotateCcw size={13} /> {entry.status === 'Reconciled' ? 'Desfazer' : 'Reabrir'}</button></div>}</article>
}

function HelpCenterPage({ user, workspace, showToast }: { user: AuthUser; workspace?: WorkspaceData; showToast: (value: string) => void }) {
  const [articles, setArticles] = useState<HelpArticleData[]>([])
  const [mine, setMine] = useState<SupportTicketData[]>([])
  const [assigned, setAssigned] = useState<SupportTicketData[]>([])
  const [tab, setTab] = useState<'articles' | 'mine' | 'assigned'>('articles')
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('Todos')
  const [selectedArticle, setSelectedArticle] = useState<HelpArticleData | null>(null)
  const [selectedTicket, setSelectedTicket] = useState<SupportTicketData | null>(null)
  const [newTicket, setNewTicket] = useState(false)
  const [loading, setLoading] = useState(true)
  const canSeeAssigned = (workspace?.role === 'Owner' || workspace?.role === 'Administrator' || user.globalRole !== 'User') && (user.globalRole === 'User' || user.isTwoFactorEnabled)
  const load = useCallback(async () => {
    setLoading(true)
    try {
      const requests = [apiFetch('/api/help/articles'), apiFetch('/api/help/tickets?scope=mine')]
      if (canSeeAssigned) requests.push(apiFetch('/api/help/tickets?scope=assigned'))
      const responses = await Promise.all(requests)
      if (responses.some(response => !response.ok)) throw new Error()
      setArticles(await responses[0].json() as HelpArticleData[]); setMine(await responses[1].json() as SupportTicketData[])
      if (responses[2]) setAssigned(await responses[2].json() as SupportTicketData[])
    } catch { showToast('Não foi possível carregar a Central de Ajuda') }
    finally { setLoading(false) }
  }, [canSeeAssigned, showToast])
  /* oxlint-disable react/set-state-in-effect */
  useEffect(() => { void load() }, [workspace?.id, load])
  /* oxlint-enable react/set-state-in-effect */
  const normalized = query.trim().toLocaleLowerCase('pt-BR')
  const filteredArticles = articles.filter(article => (category === 'Todos' || article.category === category) && (!normalized || `${article.title} ${article.summary} ${article.keywords.join(' ')} ${article.steps.join(' ')}`.toLocaleLowerCase('pt-BR').includes(normalized)))
  const categories = ['Todos', ...new Set(articles.map(article => article.category))]
  const tickets = tab === 'assigned' ? assigned : mine
  const openCount = tickets.filter(ticket => !['Resolved', 'Closed'].includes(ticket.status)).length
  const refreshed = async (ticket?: SupportTicketData) => { if (ticket) setSelectedTicket(ticket); await load() }
  return <div className="page help-page"><section className="page-heading help-heading"><div><p className="eyebrow">SUPORTE E ORIENTAÇÃO</p><h1>Central de ajuda</h1><p>Encontre respostas ou converse com a pessoa certa sem sair do Nexo.</p></div><button className="primary-button" onClick={() => setNewTicket(true)}><MessageCircle size={17} /> Abrir chamado</button></section>
    <section className="help-hero"><div><HelpCircle size={22} /><span><strong>Como podemos ajudar?</strong><small>Pesquise por uma dúvida, tarefa ou mensagem de erro.</small></span></div><label><Search size={17} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Ex.: importar OFX, trocar senha, saldo divergente…" /></label></section>
    <div className="help-tabs"><button className={tab === 'articles' ? 'active' : ''} onClick={() => setTab('articles')}><BookOpen size={15} /> Artigos</button><button className={tab === 'mine' ? 'active' : ''} onClick={() => setTab('mine')}><MessageCircle size={15} /> Meus chamados <em>{mine.length}</em></button>{canSeeAssigned && <button className={tab === 'assigned' ? 'active' : ''} onClick={() => setTab('assigned')}><Headphones size={15} /> Para atender <em>{assigned.filter(ticket => !['Resolved', 'Closed'].includes(ticket.status)).length}</em></button>}</div>
    {loading && <div className="platform-loading">Carregando a Central de Ajuda…</div>}
    {!loading && tab === 'articles' && <><div className="help-categories">{categories.map(item => <button className={category === item ? 'active' : ''} onClick={() => setCategory(item)} key={item}>{item}</button>)}</div><section className="help-article-grid">{filteredArticles.map(article => <button onClick={() => setSelectedArticle(article)} key={article.id}><span><BookOpen size={17} /></span><small>{article.category}</small><strong>{article.title}</strong><p>{article.summary}</p><em>Ler guia <ArrowRight size={13} /></em></button>)}{filteredArticles.length === 0 && <div className="help-empty"><Search size={23} /><strong>Nenhum artigo encontrado</strong><span>Você pode abrir um chamado com essa dúvida.</span><button onClick={() => setNewTicket(true)}>Abrir chamado</button></div>}</section></>}
    {!loading && tab !== 'articles' && <section className="ticket-panel"><header><div><h2>{tab === 'mine' ? 'Meus chamados' : 'Fila de atendimento'}</h2><p>{openCount} chamado(s) em acompanhamento</p></div><button className="secondary-button" onClick={() => void load()}><RotateCcw size={14} /> Atualizar</button></header><div className="ticket-list">{tickets.map(ticket => <button onClick={() => setSelectedTicket(ticket)} key={ticket.id}><span className={`ticket-priority ${ticket.priority.toLowerCase()}`} /><div><strong>{ticket.subject}</strong><small>#{ticket.id.slice(0, 8).toUpperCase()} · {ticket.workspaceName} · {ticket.requesterName}</small></div><em className={`ticket-target ${ticket.target.toLowerCase()}`}>{ticket.target === 'WorkspaceAdministration' ? 'Administração' : ticket.target === 'Support' ? 'Suporte' : 'Desenvolvimento'}</em><span className={`ticket-status ${ticket.status.toLowerCase()}`}>{ticket.status === 'Open' ? 'Aberto' : ticket.status === 'InProgress' ? 'Em atendimento' : ticket.status === 'WaitingRequester' ? 'Aguardando você' : ticket.status === 'Resolved' ? 'Resolvido' : 'Fechado'}</span><time>{new Date(ticket.updatedAt).toLocaleDateString('pt-BR')}</time></button>)}{tickets.length === 0 && <div className="help-empty"><CheckCircle2 size={24} /><strong>{tab === 'mine' ? 'Nenhum chamado aberto' : 'Fila vazia'}</strong><span>{tab === 'mine' ? 'Se precisar, fale com a administração, suporte ou desenvolvimento.' : 'Não há solicitações destinadas ao seu perfil.'}</span></div>}</div></section>}
    {selectedArticle && <HelpArticleModal article={selectedArticle} onClose={() => setSelectedArticle(null)} onCreateTicket={() => { setSelectedArticle(null); setNewTicket(true) }} />}
    {newTicket && <NewSupportTicketModal onClose={() => setNewTicket(false)} onSaved={async ticket => { setNewTicket(false); setTab('mine'); setSelectedTicket(ticket); await refreshed(); showToast('Chamado aberto com sucesso') }} />}
    {selectedTicket && <SupportTicketModal ticket={selectedTicket} currentUser={user} assignedView={tab === 'assigned'} onClose={() => setSelectedTicket(null)} onChanged={refreshed} showToast={showToast} />}
  </div>
}

function HelpArticleModal({ article, onClose, onCreateTicket }: { article: HelpArticleData; onClose: () => void; onCreateTicket: () => void }) {
  return <div className="modal-backdrop" onMouseDown={onClose}><section className="entry-modal help-article-modal" role="dialog" aria-modal="true" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>{article.category.toUpperCase()}</p><h2>{article.title}</h2></div><button onClick={onClose} aria-label="Fechar"><X size={20} /></button></div><p>{article.summary}</p><ol>{article.steps.map((step, index) => <li key={step}><span>{index + 1}</span>{step}</li>)}</ol><div className="article-help-footer"><span>Este guia não resolveu?</span><button className="secondary-button" onClick={onCreateTicket}><MessageCircle size={14} /> Abrir chamado</button></div></section></div>
}

function NewSupportTicketModal({ onClose, onSaved }: { onClose: () => void; onSaved: (ticket: SupportTicketData) => Promise<void> }) {
  const [target, setTarget] = useState<SupportTicketData['target']>('WorkspaceAdministration')
  const [subject, setSubject] = useState(''); const [category, setCategory] = useState('Dúvida de uso'); const [priority, setPriority] = useState<SupportTicketData['priority']>('Normal'); const [description, setDescription] = useState(''); const [consent, setConsent] = useState(false); const [busy, setBusy] = useState(false); const [error, setError] = useState('')
  const submit = async (event: React.FormEvent) => { event.preventDefault(); setBusy(true); setError(''); try { const response = await apiFetch('/api/help/tickets', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ target, subject, category, priority, description, consentToShareWorkspaceData: consent }) }); const result = await response.json().catch(() => null) as SupportTicketData & { detail?: string }; if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível abrir o chamado.'); await onSaved(result) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado'); setBusy(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal ticket-create-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>NOVO ATENDIMENTO</p><h2>Abrir chamado</h2></div><button type="button" onClick={onClose} aria-label="Fechar"><X size={20} /></button></div><label>Enviar para<select value={target} onChange={event => setTarget(event.target.value as SupportTicketData['target'])}><option value="WorkspaceAdministration">Administração do espaço</option><option value="Support">Suporte geral do Nexo</option><option value="Development">Equipe de desenvolvimento</option></select></label><div className="ticket-routing-note">{target === 'WorkspaceAdministration' ? 'Proprietários e administradores deste espaço poderão responder.' : target === 'Support' ? 'Use para acesso, cobrança, uso da plataforma ou orientação geral.' : 'Use para erros técnicos, comportamento inesperado ou sugestões do produto.'}</div><label>Assunto<input value={subject} onChange={event => setSubject(event.target.value)} maxLength={140} placeholder="Resuma o que você precisa" autoFocus /></label><div className="ticket-form-row"><label>Categoria<select value={category} onChange={event => setCategory(event.target.value)}><option>Dúvida de uso</option><option>Problema técnico</option><option>Acesso e segurança</option><option>Dados financeiros</option><option>Sugestão</option><option>Outro</option></select></label><label>Prioridade<select value={priority} onChange={event => setPriority(event.target.value as SupportTicketData['priority'])}><option value="Low">Baixa</option><option value="Normal">Normal</option><option value="High">Alta</option><option value="Urgent">Urgente</option></select></label></div><label>Descrição<textarea value={description} onChange={event => setDescription(event.target.value)} maxLength={4000} rows={6} placeholder="Explique o contexto, o resultado esperado e o que aconteceu." /></label>{target !== 'WorkspaceAdministration' && <label className="ticket-consent"><input type="checkbox" checked={consent} onChange={event => setConsent(event.target.checked)} /><span><strong>Autorizar análise dos dados deste espaço</strong><small>O suporte ou desenvolvimento só poderá consultar dados financeiros adicionais quando esta autorização estiver marcada.</small></span></label>}{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={busy}>{busy ? 'Abrindo…' : 'Abrir chamado'}</button></div></form></div>
}

function SupportTicketModal({ ticket, currentUser, assignedView, onClose, onChanged, showToast }: { ticket: SupportTicketData; currentUser: AuthUser; assignedView: boolean; onClose: () => void; onChanged: (ticket?: SupportTicketData) => Promise<void>; showToast: (value: string) => void }) {
  const [message, setMessage] = useState(''); const [busy, setBusy] = useState(false)
  const send = async () => { if (!message.trim()) return; setBusy(true); try { const response = await apiFetch(`/api/help/tickets/${ticket.id}/messages`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ body: message }) }); const result = await response.json().catch(() => null) as SupportTicketData & { detail?: string }; if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível enviar.'); setMessage(''); await onChanged(result) } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') } finally { setBusy(false) } }
  const changeStatus = async (status: SupportTicketData['status']) => { setBusy(true); try { const response = await apiFetch(`/api/help/tickets/${ticket.id}/status`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }) }); const result = await response.json().catch(() => null) as SupportTicketData & { detail?: string }; if (!response.ok) throw new Error(result?.detail ?? 'Não foi possível alterar.'); await onChanged(result); showToast('Situação do chamado atualizada') } catch (reason) { showToast(reason instanceof Error ? reason.message : 'Erro inesperado') } finally { setBusy(false) } }
  return <div className="modal-backdrop" onMouseDown={onClose}><section className="entry-modal ticket-detail-modal" role="dialog" aria-modal="true" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>CHAMADO #{ticket.id.slice(0, 8).toUpperCase()}</p><h2>{ticket.subject}</h2><span>{ticket.workspaceName} · {ticket.requesterName}</span></div><button onClick={onClose} aria-label="Fechar"><X size={20} /></button></div><div className="ticket-detail-meta"><span className={`ticket-status ${ticket.status.toLowerCase()}`}>{ticket.status === 'Open' ? 'Aberto' : ticket.status === 'InProgress' ? 'Em atendimento' : ticket.status === 'WaitingRequester' ? 'Aguardando solicitante' : ticket.status === 'Resolved' ? 'Resolvido' : 'Fechado'}</span><span>{ticket.target === 'WorkspaceAdministration' ? 'Administração' : ticket.target === 'Support' ? 'Suporte geral' : 'Desenvolvimento'}</span><span>Prioridade {ticket.priority === 'Low' ? 'baixa' : ticket.priority === 'Normal' ? 'normal' : ticket.priority === 'High' ? 'alta' : 'urgente'}</span>{ticket.consentToShareWorkspaceData && <span className="consent-badge"><ShieldCheck size={12} /> Consulta autorizada</span>}</div><div className="ticket-conversation">{ticket.messages.map(item => <article className={item.authorUserId === currentUser.id ? 'mine' : ''} key={item.id}><header><strong>{item.authorName}</strong><time>{new Date(item.createdAt).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}</time></header><p>{item.body}</p></article>)}</div>{assignedView && <div className="ticket-status-actions"><span>Atualizar situação</span><select value={ticket.status} disabled={busy} onChange={event => void changeStatus(event.target.value as SupportTicketData['status'])}><option value="Open">Aberto</option><option value="InProgress">Em atendimento</option><option value="WaitingRequester">Aguardando solicitante</option><option value="Resolved">Resolvido</option><option value="Closed">Fechado</option></select></div>}<div className="ticket-reply"><textarea value={message} onChange={event => setMessage(event.target.value)} rows={3} maxLength={4000} placeholder="Escreva uma resposta…" disabled={busy || ticket.status === 'Closed'} /><button className="primary-button" onClick={() => void send()} disabled={busy || !message.trim() || ticket.status === 'Closed'}><Send size={15} /> Enviar</button></div></section></div>
}

function CashFlowPage() {
  const [days, setDays] = useState(30)
  const [data, setData] = useState<CashFlowData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  // O período selecionado sincroniza esta visualização com a API.
  /* oxlint-disable react/set-state-in-effect */
  useEffect(() => {
    let active = true
    setLoading(true)
    apiFetch(`/api/cash-flow?days=${days}`).then(async response => {
      if (!response.ok) throw new Error('Não foi possível carregar o fluxo de caixa.')
      return response.json() as Promise<CashFlowData>
    }).then(result => { if (active) { setData(result); setError(''); setLoading(false) } })
      .catch(reason => { if (active) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.'); setLoading(false) } })
    return () => { active = false }
  }, [days])
  /* oxlint-enable react/set-state-in-effect */

  const points = data?.points ?? []
  const sampleEvery = Math.max(1, Math.ceil(points.length / 12))
  const sampled = points.filter((_, index) => index % sampleEvery === 0 || index === points.length - 1)
  const maxMovement = Math.max(1, ...sampled.flatMap(point => [point.income + point.projectedIncome, point.expense + point.projectedExpense]))
  const balances = sampled.map(point => point.balance)
  const minBalance = Math.min(...balances, 0)
  const maxBalance = Math.max(...balances, 1)
  const movementDays = points.filter(point => point.income + point.projectedIncome > 0 || point.expense + point.projectedExpense > 0).reverse()
  const dateLabel = (date: string, full = false) => new Date(`${date}T12:00:00`).toLocaleDateString('pt-BR', full ? { day: '2-digit', month: 'short', year: 'numeric' } : { day: '2-digit', month: 'short' })

  return <div className="page cashflow-page">
    <section className="page-heading cashflow-heading"><div><p className="eyebrow">PLANEJAMENTO</p><h1>Fluxo de caixa</h1><p>Acompanhe a movimentação e a evolução real do seu saldo.</p></div><div className="period-tabs" aria-label="Período do fluxo de caixa">{[30, 60, 90].map(period => <button className={days === period ? 'active' : ''} onClick={() => setDays(period)} key={period}>{period} dias</button>)}</div></section>
    {error && <div className="cashflow-error"><BarChart3 size={19} /><div><strong>Fluxo indisponível</strong><span>{error} Verifique se a API está iniciada.</span></div></div>}
    {loading && <div className="cashflow-loading"><i /><i /><i /></div>}
    {!loading && data && <>
      <section className="cash-kpis"><article><span>Saldo atual</span><strong>{currency(data.currentBalance)}</strong><small>Posição realizada hoje</small></article><article><span>Entradas realizadas</span><strong className="positive-text">+ {currency(data.totalIncome)}</strong><small>Mais {currency(data.projectedIncome)} previstas</small></article><article><span>Saídas realizadas</span><strong className="negative-text">− {currency(data.totalExpense)}</strong><small>Mais {currency(data.projectedExpense)} previstas</small></article><article className="current-balance"><span>Saldo projetado</span><strong>{currency(data.projectedBalance)}</strong><small><i className={data.projectedBalance >= data.currentBalance ? 'up' : 'down'}>{data.projectedBalance >= data.currentBalance ? <TrendingUp size={12} /> : <TrendingDown size={12} />}</i> Horizonte de {days} dias</small></article></section>
      <section className="cashflow-layout">
        <article className="panel cashflow-chart-panel"><div className="cashflow-panel-head"><div><h3>Evolução do saldo</h3><p>{dateLabel(data.startDate)} até {dateLabel(data.endDate)}</p></div><div className="chart-legend"><span><i className="legend-bar income" />Entradas</span><span><i className="legend-bar expense" />Saídas</span><span><i className="legend-dot" />Saldo</span></div></div>
          <div className="cashflow-chart"><div className="cash-chart-grid"><i /><i /><i /><i /></div>{sampled.map(point => { const balancePosition = ((point.balance - minBalance) / (maxBalance - minBalance || 1)) * 72 + 12; return <div className={`cash-column ${point.isProjected ? 'projected' : ''}`} key={point.date}><div className="balance-dot" style={{ bottom: `${balancePosition}%` }} title={`Saldo ${currency(point.balance)}`} /><div className="cash-bars"><i className="cash-income" style={{ height: `${((point.income + point.projectedIncome) / maxMovement) * 68}%` }} /><i className="cash-expense" style={{ height: `${((point.expense + point.projectedExpense) / maxMovement) * 68}%` }} /></div><span>{new Date(`${point.date}T12:00:00`).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' })}</span></div> })}</div>
          <div className="cashflow-insight"><Sparkles size={18} /><div><strong>{data.netResult >= 0 ? 'Caixa saudável no período' : 'Atenção ao consumo de caixa'}</strong><span>{data.netResult >= 0 ? `As entradas superaram as saídas em ${currency(data.netResult)}.` : `As saídas superaram as entradas em ${currency(data.netResult)}.`}</span></div></div>
        </article>
        <article className="panel cashflow-days"><div className="cashflow-panel-head"><div><h3>Dias com movimento</h3><p>Realizado e previsto</p></div><span className="movement-count">{movementDays.length}</span></div><div className="daily-list">{movementDays.slice(0, 8).map(point => { const income = point.income + point.projectedIncome; const expense = point.expense + point.projectedExpense; return <div className={`daily-row ${point.isProjected ? 'projected' : ''}`} key={point.date}><span className="daily-date"><b>{new Date(`${point.date}T12:00:00`).getDate().toString().padStart(2, '0')}</b><small>{new Date(`${point.date}T12:00:00`).toLocaleDateString('pt-BR', { month: 'short' }).replace('.', '').toUpperCase()}</small></span><div><span>Entradas <b className="positive-text">{currency(income)}</b></span><span>Saídas <b className="negative-text">{currency(expense)}</b></span></div><strong>{income - expense >= 0 ? '+' : '−'} {currency(income - expense)}{point.isProjected && <small>Previsto</small>}</strong></div> })}{movementDays.length === 0 && <div className="empty-cashflow"><WalletCards size={25} /><strong>Nenhum movimento</strong><span>Cadastre receitas ou despesas neste período.</span></div>}</div></article>
      </section>
    </>}
  </div>
}

function WorkspaceAccessPage({ user, workspace, onWorkspacesChanged, showToast }: { user: AuthUser; workspace?: WorkspaceData; onWorkspacesChanged: () => Promise<WorkspaceData[]>; showToast: (value: string) => void }) {
  const [members, setMembers] = useState<WorkspaceMemberData[]>([])
  const [sentInvitations, setSentInvitations] = useState<WorkspaceInvitationData[]>([])
  const [myInvitations, setMyInvitations] = useState<WorkspaceInvitationData[]>([])
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<'Administrator' | 'Member'>('Member')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const canManage = workspace?.role === 'Owner' || workspace?.role === 'Administrator'
  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const invitationResponse = await apiFetch('/api/workspace-invitations'); if (!invitationResponse.ok) throw new Error('Não foi possível consultar seus convites.'); setMyInvitations(await invitationResponse.json() as WorkspaceInvitationData[])
      if (workspace && canManage) { const [membersResponse, sentResponse] = await Promise.all([apiFetch(`/api/workspaces/${workspace.id}/members`), apiFetch(`/api/workspaces/${workspace.id}/invitations`)]); if (!membersResponse.ok || !sentResponse.ok) throw new Error('Não foi possível carregar a equipe.'); setMembers(await membersResponse.json() as WorkspaceMemberData[]); setSentInvitations(await sentResponse.json() as WorkspaceInvitationData[]) }
      else { setMembers([]); setSentInvitations([]) }
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.') }
    finally { setLoading(false) }
  }, [workspace, canManage])
  // A requisição externa compartilha o estado de carregamento com as ações de atualização.
  // oxlint-disable-next-line react/set-state-in-effect
  useEffect(() => { void load() }, [load])
  const invite = async (event: React.FormEvent) => { event.preventDefault(); if (!workspace) return; const response = await apiFetch(`/api/workspaces/${workspace.id}/invitations`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, role }) }); const result = await response.json().catch(() => null) as { detail?: string } | null; if (!response.ok) { setError(result?.detail ?? 'Não foi possível convidar.'); return } setEmail(''); await load(); showToast('Convite criado com validade de 7 dias') }
  const accept = async (invitation: WorkspaceInvitationData) => { const response = await apiFetch(`/api/workspace-invitations/${invitation.id}/accept`, { method: 'POST' }); if (!response.ok) { showToast('Não foi possível aceitar o convite.'); return } await onWorkspacesChanged(); await load(); showToast(`Você entrou em ${invitation.workspaceName}`) }
  const revoke = async (invitation: WorkspaceInvitationData) => { if (!workspace) return; const response = await apiFetch(`/api/workspaces/${workspace.id}/invitations/${invitation.id}`, { method: 'DELETE' }); if (!response.ok) { showToast('Não foi possível revogar o convite.'); return } await load(); showToast('Convite revogado') }
  const changeRole = async (member: WorkspaceMemberData, nextRole: 'Administrator' | 'Member') => { if (!workspace) return; const response = await apiFetch(`/api/workspaces/${workspace.id}/members/${member.userId}/role`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ role: nextRole }) }); if (!response.ok) { const result = await response.json().catch(() => null) as { detail?: string } | null; showToast(result?.detail ?? 'Não foi possível alterar o papel.'); return } await load(); showToast('Permissão atualizada') }
  const remove = async (member: WorkspaceMemberData) => { if (!workspace || !window.confirm(`Remover ${member.name} deste espaço?`)) return; const response = await apiFetch(`/api/workspaces/${workspace.id}/members/${member.userId}`, { method: 'DELETE' }); if (!response.ok) { const result = await response.json().catch(() => null) as { detail?: string } | null; showToast(result?.detail ?? 'Não foi possível remover.'); return } await load(); showToast('Membro removido') }
  const transfer = async (member: WorkspaceMemberData) => { if (!workspace || !window.confirm(`Transferir a propriedade de ${workspace.name} para ${member.name}? Você passará a administrador.`)) return; const response = await apiFetch(`/api/workspaces/${workspace.id}/transfer-ownership`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ newOwnerId: member.userId }) }); if (!response.ok) { const result = await response.json().catch(() => null) as { detail?: string } | null; showToast(result?.detail ?? 'Não foi possível transferir.'); return } await onWorkspacesChanged(); await load(); showToast('Propriedade transferida') }
  const roleLabels = { Owner: 'Proprietário', Administrator: 'Administrador', Member: 'Membro' }
  return <div className="page workspace-access-page"><section className="page-heading"><div><p className="eyebrow">CONTROLE DE ACESSO</p><h1>Equipe e acessos</h1><p>Convide pessoas e defina o que cada uma pode fazer em {workspace?.name ?? 'seu espaço'}.</p></div><span className="privileged-badge"><ShieldCheck size={16} /> {workspace ? roleLabels[workspace.role] : 'Sem espaço'}</span></section>
    {myInvitations.length > 0 && <section className="pending-invitations"><div><h2>Convites para você</h2><p>Aceite para adicionar o espaço à sua navegação.</p></div>{myInvitations.map(item => <article key={item.id}><span><Building2 size={18} /></span><div><strong>{item.workspaceName}</strong><small>Acesso como {roleLabels[item.role]}</small></div><time>Expira em {new Date(item.expiresAt).toLocaleDateString('pt-BR')}</time><button className="primary-button" onClick={() => void accept(item)}>Aceitar</button></article>)}</section>}
    {error && <div className="settings-error">{error}<button onClick={() => setError('')}><X size={15} /></button></div>}{loading ? <div className="platform-loading">Carregando equipe…</div> : !canManage ? <section className="readonly-access"><ShieldCheck size={25} /><h2>Acesso somente leitura</h2><p>Como membro, você pode consultar os dados financeiros. Alterações ficam reservadas a administradores e proprietários.</p></section> : <div className="access-layout"><section className="list-panel member-panel"><div className="account-panel-head"><div><h3>Membros do espaço</h3><p>{members.length} pessoas com acesso</p></div></div><div className="member-list">{members.map(member => <article key={member.userId}><span className="platform-avatar">{member.name.split(' ').map(part => part[0]).join('').slice(0, 2).toUpperCase()}</span><div><strong>{member.name}</strong><small>{member.email}</small></div><span className={`member-role ${member.role.toLowerCase()}`}>{roleLabels[member.role]}</span>{workspace?.role === 'Owner' && member.role !== 'Owner' ? <select value={member.role} onChange={event => void changeRole(member, event.target.value as 'Administrator' | 'Member')}><option value="Administrator">Administrador</option><option value="Member">Membro</option></select> : <span /> }<div className="member-actions">{workspace?.role === 'Owner' && member.userId !== user.id && <button onClick={() => void transfer(member)} title="Transferir propriedade"><ShieldCheck size={14} /></button>}{member.role !== 'Owner' && member.userId !== user.id && <button className="danger-link" onClick={() => void remove(member)} title="Remover membro"><Ban size={14} /></button>}</div></article>)}</div></section><aside className="invite-panel"><h2>Convidar pessoa</h2><p>O convite fica disponível por sete dias para uma conta com o mesmo e-mail.</p><form onSubmit={invite}><label>E-mail<input type="email" value={email} onChange={event => setEmail(event.target.value)} placeholder="pessoa@empresa.com" required /></label><label>Papel<select value={role} onChange={event => setRole(event.target.value as typeof role)}>{workspace?.role === 'Owner' && <option value="Administrator">Administrador</option>}<option value="Member">Membro</option></select></label><button className="primary-button">Enviar convite</button></form><div className="permission-matrix"><strong>Permissões</strong><span><b>Proprietário</b> controle total e transferência.</span><span><b>Administrador</b> operação financeira e gestão de membros.</span><span><b>Membro</b> consulta em modo somente leitura.</span></div></aside></div>}
    {canManage && sentInvitations.length > 0 && <section className="sent-invitations list-panel"><div className="account-panel-head"><div><h3>Histórico de convites</h3><p>Convites recentes deste espaço</p></div></div>{sentInvitations.map(item => <article key={item.id}><div><strong>{item.email}</strong><small>{roleLabels[item.role]} · expira em {new Date(item.expiresAt).toLocaleDateString('pt-BR')}</small></div><span className={`invitation-state ${item.status.toLowerCase()}`}>{item.status === 'Pending' ? 'Pendente' : item.status === 'Accepted' ? 'Aceito' : item.status === 'Revoked' ? 'Revogado' : 'Expirado'}</span>{item.status === 'Pending' && <button className="secondary-button" onClick={() => void revoke(item)}>Revogar</button>}</article>)}</section>}
  </div>
}

function PlatformAdminPage({ currentUser, showToast }: { currentUser: AuthUser; showToast: (value: string) => void }) {
  const [overview, setOverview] = useState<PlatformOverviewData | null>(null)
  const [users, setUsers] = useState<PlatformUserData[]>([])
  const [workspaces, setWorkspaces] = useState<PlatformWorkspaceData[]>([])
  const [audit, setAudit] = useState<PlatformAuditData[]>([])
  const [tab, setTab] = useState<'users' | 'workspaces' | 'audit'>('users')
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [reset, setReset] = useState<{ user: PlatformUserData; token?: string; expiresAt?: string } | null>(null)
  const load = useCallback(async () => {
    setLoading(true); setError('')
    try {
      const [overviewResponse, usersResponse, workspacesResponse, auditResponse] = await Promise.all([apiFetch('/api/platform/overview'), apiFetch('/api/platform/users'), apiFetch('/api/platform/workspaces'), apiFetch('/api/platform/audit')])
      if (![overviewResponse, usersResponse, workspacesResponse, auditResponse].every(response => response.ok)) throw new Error('Não foi possível carregar a administração da plataforma.')
      setOverview(await overviewResponse.json() as PlatformOverviewData); setUsers(await usersResponse.json() as PlatformUserData[]); setWorkspaces(await workspacesResponse.json() as PlatformWorkspaceData[]); setAudit(await auditResponse.json() as PlatformAuditData[])
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.') }
    finally { setLoading(false) }
  }, [])
  // A requisição externa compartilha o estado de carregamento com as ações de atualização.
  // oxlint-disable-next-line react/set-state-in-effect
  useEffect(() => { void load() }, [load])
  const changeRole = async (user: PlatformUserData, role: GlobalRole) => {
    const response = await apiFetch(`/api/platform/users/${user.id}/role`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ role }) })
    if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; showToast(problem?.detail ?? 'Não foi possível alterar o papel.'); return }
    await load(); showToast('Papel global atualizado')
  }
  const toggleUser = async (user: PlatformUserData) => {
    const response = await apiFetch(`/api/platform/users/${user.id}/active`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ active: !user.isActive }) })
    if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; showToast(problem?.detail ?? 'Não foi possível alterar o usuário.'); return }
    await load(); showToast(user.isActive ? 'Usuário bloqueado e sessões invalidadas' : 'Usuário reativado')
  }
  const issueReset = async (user: PlatformUserData) => {
    const response = await apiFetch(`/api/platform/users/${user.id}/password-reset`, { method: 'POST' })
    const result = await response.json().catch(() => null) as { token?: string; expiresAt?: string; detail?: string } | null
    if (!response.ok) { showToast(result?.detail ?? 'Não foi possível iniciar a redefinição.'); return }
    setReset({ user, token: result?.token, expiresAt: result?.expiresAt }); await load()
  }
  const filteredUsers = users.filter(item => `${item.name} ${item.email} ${item.globalRole}`.toLowerCase().includes(query.toLowerCase()))
  const roleLabels: Record<GlobalRole, string> = { User: 'Usuário', Support: 'Suporte', Developer: 'Desenvolvedor', SuperAdmin: 'Superadministrador' }
  return <div className="page platform-page"><section className="page-heading"><div><p className="eyebrow">PLATAFORMA</p><h1>Administração geral</h1><p>Gerencie acessos globais sem interferir silenciosamente nos dados financeiros.</p></div><span className="privileged-badge"><ShieldCheck size={16} /> {roleLabels[currentUser.globalRole]}</span></section>
    {currentUser.globalRole === 'SuperAdmin' && <BackupOperationsPanel showToast={showToast} />}
    {overview && <section className="platform-kpis"><article><small>Usuários</small><strong>{overview.activeUserCount}/{overview.userCount}</strong><span>ativos</span></article><article><small>Espaços</small><strong>{overview.workspaceCount}</strong><span>ambientes isolados</span></article><article><small>Administradores</small><strong>{overview.superAdminCount}</strong><span>acessos globais</span></article><article><small>Eventos críticos</small><strong>{overview.auditEventCount}</strong><span>ações auditadas</span></article></section>}
    <section className="list-panel platform-panel"><div className="platform-tabs"><button className={tab === 'users' ? 'active' : ''} onClick={() => setTab('users')}>Usuários</button><button className={tab === 'workspaces' ? 'active' : ''} onClick={() => setTab('workspaces')}>Espaços</button><button className={tab === 'audit' ? 'active' : ''} onClick={() => setTab('audit')}>Auditoria global</button></div>{error && <p className="platform-error">{error}</p>}{loading && <div className="platform-loading">Carregando controles da plataforma…</div>}
      {!loading && tab === 'users' && <><div className="list-toolbar"><label className="search-box"><Search size={18} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar usuário ou e-mail" /></label></div><div className="platform-user-list">{filteredUsers.map(user => <article className={!user.isActive ? 'inactive' : ''} key={user.id}><span className="platform-avatar">{user.name.split(' ').map(part => part[0]).join('').slice(0, 2).toUpperCase()}</span><div><strong>{user.name}</strong><small>{user.email} · {user.workspaceCount} {user.workspaceCount === 1 ? 'espaço' : 'espaços'}</small></div><span className={`user-state ${user.isActive ? 'active' : ''}`}>{user.isActive ? 'Ativo' : 'Bloqueado'}</span>{currentUser.globalRole === 'SuperAdmin' ? <select value={user.globalRole} disabled={user.id === currentUser.id} onChange={event => void changeRole(user, event.target.value as GlobalRole)}><option value="User">Usuário</option><option value="Support">Suporte</option><option value="Developer">Desenvolvedor</option><option value="SuperAdmin">Superadministrador</option></select> : <em>{roleLabels[user.globalRole]}</em>}<div className="platform-user-actions"><button onClick={() => void issueReset(user)} disabled={!user.isActive}><LockKeyhole size={14} /> Redefinir senha</button>{currentUser.globalRole === 'SuperAdmin' && user.id !== currentUser.id && <button className={user.isActive ? 'danger-link' : ''} onClick={() => void toggleUser(user)}>{user.isActive ? <Ban size={14} /> : <RotateCcw size={14} />}{user.isActive ? 'Bloquear' : 'Reativar'}</button>}</div></article>)}</div></>}
      {!loading && tab === 'workspaces' && <div className="platform-workspaces">{workspaces.map(workspace => <article key={workspace.id}><span><Building2 size={18} /></span><div><strong>{workspace.name}</strong><small>{workspace.kind === 'Personal' ? 'Pessoal' : 'Empresa'} · criado em {new Date(workspace.createdAt).toLocaleDateString('pt-BR')}</small></div><b>{workspace.memberCount} {workspace.memberCount === 1 ? 'membro' : 'membros'}</b></article>)}</div>}
      {!loading && tab === 'audit' && <div className="audit-list">{audit.map(item => <article className="audit-row" key={item.id}><span className="audit-action"><ShieldCheck size={16} /></span><div><strong>{item.description}</strong><small>{item.actorName} · {item.action}{item.ipAddress ? ` · IP ${item.ipAddress}` : ''}</small></div><time>{new Date(item.occurredAt).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}</time></article>)}{audit.length === 0 && <div className="contact-empty"><ShieldCheck size={27} /><strong>Nenhuma ação privilegiada</strong><span>Alterações administrativas aparecerão aqui.</span></div>}</div>}
    </section>
    {reset && <div className="modal-backdrop" onMouseDown={() => setReset(null)}><div className="entry-modal reset-result-modal" onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>Redefinição segura</p><h2>Código para {reset.user.name}</h2></div><button onClick={() => setReset(null)}><X size={20} /></button></div>{reset.token ? <><p>Envie este código ao usuário por um canal seguro. Ele expira em 20 minutos e só pode ser usado uma vez.</p><code>{reset.token}</code><button className="primary-button" onClick={() => { void navigator.clipboard.writeText(reset.token!); showToast('Código copiado') }}>Copiar código</button></> : <p>O processo foi iniciado. O código será entregue pelo provedor configurado.</p>}</div></div>}
  </div>
}

function BackupOperationsPanel({ showToast }: { showToast: (value: string) => void }) {
  const [backups, setBackups] = useState<{ enabled: boolean; lastAttemptFailed: boolean; files: { name: string; size: number; createdAt: string }[] } | null>(null)
  const [emailStatus, setEmailStatus] = useState<{ enabled: boolean; pending: number; failed: number; expired: number; sent: number } | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const load = useCallback(async () => {
    try {
      const [backupResponse, emailResponse] = await Promise.all([apiFetch('/api/platform/backups'), apiFetch('/api/platform/email/status')])
      if (!backupResponse.ok || !emailResponse.ok) throw new Error('Não foi possível consultar backups e e-mails.')
      setBackups(await backupResponse.json()); setEmailStatus(await emailResponse.json()); setError('')
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro ao consultar serviços.') }
  }, [])
  // A requisição externa compartilha o estado de carregamento com as ações de atualização.
  // oxlint-disable-next-line react/set-state-in-effect
  useEffect(() => { void load() }, [load])
  const run = async (name?: string) => {
    setBusy(true); setError('')
    try {
      const response = await apiFetch(name ? `/api/platform/backups/${encodeURIComponent(name)}/verify` : '/api/platform/backups', { method: 'POST' })
      if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(problem?.detail ?? 'Não foi possível concluir a operação.') }
      await load(); showToast(name ? 'Backup verificado com sucesso' : 'Backup criado e verificado com sucesso')
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro ao operar backup.') }
    finally { setBusy(false) }
  }
  return <section className="list-panel backup-operations">
    <div className="settings-section-head"><div><h2>Backups e e-mails</h2><p>Proteja seus dados e acompanhe os avisos enviados pelo sistema.</p></div><div className="report-actions"><button className="secondary-button" disabled={busy} onClick={() => void load()}><RefreshCw size={15} /> Atualizar</button><button className="primary-button" disabled={busy} onClick={() => void run()}>{busy ? 'Processando…' : 'Criar backup agora'}</button></div></div>
    {error && <p className="form-error" role="alert">{error}</p>}
    <div className="backup-service-status"><span>Backup automático: <strong>{backups ? backups.enabled ? 'Ativado' : 'Desativado' : 'Carregando…'}</strong>{backups?.lastAttemptFailed && ' · A última tentativa falhou'}</span><span>E-mails: <strong>{emailStatus ? emailStatus.enabled ? 'Envio ativado' : 'Aguardando configuração SMTP' : 'Carregando…'}</strong></span>{emailStatus && <span>{emailStatus.pending} na fila · {emailStatus.sent} enviados · {emailStatus.failed} com falha · {emailStatus.expired} expirados</span>}</div>
    <p className="backup-instructions">Os backups incluem contas, movimentações, anexos e dados de acesso. Guarde uma cópia fora deste servidor e preserve a chave de recuperação em um local seguro. A restauração deve ser conferida em uma instalação separada.</p>
    <div className="backup-file-list">{backups?.files.slice(0, 14).map(file => <article key={file.name}><div><strong>{new Date(file.createdAt).toLocaleString('pt-BR')}</strong><small>{(file.size / 1024 / 1024).toLocaleString('pt-BR', { maximumFractionDigits: 2 })} MB · Criptografado e verificado na criação</small></div><button className="secondary-button" disabled={busy} onClick={() => void run(file.name)}><ShieldCheck size={15} /> Verificar</button><a className="secondary-button" href={`/api/platform/backups/${encodeURIComponent(file.name)}/download`}><Download size={15} /> Baixar</a></article>)}</div>
    {backups && backups.files.length === 0 && <p>Nenhum backup disponível. Crie o primeiro para proteger seus dados.</p>}
  </section>
}

function DeveloperPage({ showToast }: { showToast: (value: string) => void }) {
  const [diagnostics, setDiagnostics] = useState<PlatformDiagnosticData | null>(null)
  const [settings, setSettings] = useState<PlatformSettingData[]>([])
  const [loading, setLoading] = useState(true)
  const load = useCallback(async () => {
    try { const [diagnosticResponse, settingsResponse] = await Promise.all([apiFetch('/api/platform/diagnostics'), apiFetch('/api/platform/settings')]); if (!diagnosticResponse.ok || !settingsResponse.ok) throw new Error(); setDiagnostics(await diagnosticResponse.json() as PlatformDiagnosticData); setSettings(await settingsResponse.json() as PlatformSettingData[]) }
    catch { showToast('Não foi possível carregar a área técnica.') }
    finally { setLoading(false) }
  }, [showToast])
  // A requisição externa compartilha o estado de carregamento com as ações de atualização.
  // oxlint-disable-next-line react/set-state-in-effect
  useEffect(() => { void load() }, [load])
  const save = async (setting: PlatformSettingData) => {
    const response = await apiFetch(`/api/platform/settings/${setting.key}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ value: setting.value }) })
    if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; showToast(problem?.detail ?? 'Não foi possível salvar.'); return }
    await load(); showToast('Configuração técnica salva e auditada')
  }
  const labels: Record<string, { title: string; detail: string }> = { MaintenanceNotice: { title: 'Aviso de manutenção', detail: 'Mensagem operacional exibida pela plataforma.' }, BankImportEnabled: { title: 'Importação bancária', detail: 'Feature flag para disponibilidade da importação.' }, ExperimentalReportsEnabled: { title: 'Relatórios experimentais', detail: 'Habilita recursos de relatório em validação.' } }
  return <div className="page developer-page"><section className="page-heading"><div><p className="eyebrow">OPERAÇÃO TÉCNICA</p><h1>Área do desenvolvedor</h1><p>Diagnóstico e configurações técnicas, sem acesso global aos dados financeiros.</p></div><span className="privileged-badge technical"><Wrench size={16} /> Acesso técnico</span></section>{loading && <div className="platform-loading">Verificando serviços…</div>}{diagnostics && <section className="diagnostic-grid"><article><span className="health-dot" /><small>Estado da API</small><strong>{diagnostics.status === 'Healthy' ? 'Saudável' : diagnostics.status}</strong></article><article><Server size={18} /><small>Ambiente</small><strong>{diagnostics.environment}</strong></article><article><span className="diagnostic-number">{diagnostics.appliedMigrations}</span><small>Migrations aplicadas</small><strong>{diagnostics.pendingMigrations} pendentes</strong></article><article><span className="diagnostic-number">v</span><small>Versão</small><strong>{diagnostics.version}</strong></article></section>}<section className="technical-settings"><div className="settings-section-head"><div><h2>Configurações técnicas</h2><p>Qualquer alteração é registrada na auditoria global.</p></div></div>{settings.map(setting => <article key={setting.key}><div><strong>{labels[setting.key]?.title ?? setting.key}</strong><small>{labels[setting.key]?.detail}</small></div>{setting.key.endsWith('Enabled') ? <select value={setting.value} onChange={event => setSettings(items => items.map(item => item.key === setting.key ? { ...item, value: event.target.value } : item))}><option value="true">Ativado</option><option value="false">Desativado</option></select> : <input value={setting.value} placeholder="Nenhum aviso ativo" onChange={event => setSettings(items => items.map(item => item.key === setting.key ? { ...item, value: event.target.value } : item))} />}<button className="secondary-button" onClick={() => void save(setting)}>Salvar</button></article>)}</section></div>
}

function ContactsPage({ contacts, onChanged, showToast }: { contacts: ContactData[]; onChanged: () => Promise<void>; showToast: (value: string) => void }) {
  const [query, setQuery] = useState('')
  const [kind, setKind] = useState<'all' | ContactData['kind']>('all')
  const [showInactive, setShowInactive] = useState(false)
  const [editing, setEditing] = useState<ContactData | 'new' | null>(null)
  const [busyId, setBusyId] = useState('')
  const visible = contacts.filter(item => (showInactive || item.isActive) && (kind === 'all' || item.kind === kind || item.kind === 'Both') && `${item.name} ${item.taxId ?? ''} ${item.email ?? ''}`.toLowerCase().includes(query.trim().toLowerCase()))
  const labels = { Customer: 'Cliente', Supplier: 'Fornecedor', Both: 'Cliente e fornecedor' }
  const toggle = async (item: ContactData) => {
    setBusyId(item.id)
    try {
      const response = await apiFetch(`/api/contacts/${item.id}/active`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ active: !item.isActive }) })
      if (!response.ok) throw new Error()
      await onChanged(); showToast(item.isActive ? 'Contato arquivado' : 'Contato reativado')
    } catch { showToast('Não foi possível alterar o contato.') }
    finally { setBusyId('') }
  }
  return <div className="page contacts-page">
    <section className="page-heading"><div><p className="eyebrow">RELACIONAMENTOS</p><h1>Clientes e fornecedores</h1><p>Centralize os dados das pessoas e empresas ligadas aos seus compromissos.</p></div><button className="primary-button" onClick={() => setEditing('new')}><Plus size={17} /> Novo contato</button></section>
    <section className="list-panel"><div className="list-toolbar contacts-toolbar"><label className="search-box"><Search size={18} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar por nome, documento ou e-mail" /></label><label className="filter-field"><UsersRound size={15} /><select value={kind} onChange={event => setKind(event.target.value as typeof kind)}><option value="all">Todos os tipos</option><option value="Customer">Clientes</option><option value="Supplier">Fornecedores</option><option value="Both">Ambos</option></select></label><label className="inactive-toggle"><input type="checkbox" checked={showInactive} onChange={event => setShowInactive(event.target.checked)} /> Mostrar arquivados</label></div>
      <div className="contact-grid">{visible.map(item => <article className={`contact-card ${item.isActive ? '' : 'inactive'}`} key={item.id}><div className="contact-card-head"><span>{item.name.charAt(0).toUpperCase()}</span><em className={item.isActive ? 'active' : ''}>{item.isActive ? 'Ativo' : 'Arquivado'}</em></div><h3>{item.name}</h3><strong>{labels[item.kind]}</strong><div className="contact-details"><span>{item.taxId || 'Documento não informado'}</span><span>{item.email || 'E-mail não informado'}</span><span>{item.phone || 'Telefone não informado'}</span></div><small>{item.commitmentCount} {item.commitmentCount === 1 ? 'compromisso vinculado' : 'compromissos vinculados'}</small><div className="contact-actions"><button className="secondary-button" onClick={() => setEditing(item)}><Pencil size={14} /> Editar</button><button className={item.isActive ? 'archive-account' : 'restore-account'} disabled={busyId === item.id} onClick={() => void toggle(item)}>{item.isActive ? <Ban size={14} /> : <RotateCcw size={14} />}{item.isActive ? 'Arquivar' : 'Reativar'}</button></div></article>)}{visible.length === 0 && <div className="contact-empty"><UsersRound size={27} /><strong>Nenhum contato encontrado</strong><span>Cadastre um cliente ou fornecedor para vincular aos compromissos.</span></div>}</div>
    </section>
    {editing && <ContactModal contact={editing === 'new' ? null : editing} onClose={() => setEditing(null)} onSaved={async () => { setEditing(null); await onChanged(); showToast(editing === 'new' ? 'Contato criado' : 'Contato atualizado') }} />}
  </div>
}

function ContactModal({ contact, onClose, onSaved }: { contact: ContactData | null; onClose: () => void; onSaved: () => Promise<void> }) {
  const [name, setName] = useState(contact?.name ?? '')
  const [kind, setKind] = useState<ContactData['kind']>(contact?.kind ?? 'Customer')
  const [taxId, setTaxId] = useState(contact?.taxId ?? '')
  const [email, setEmail] = useState(contact?.email ?? '')
  const [phone, setPhone] = useState(contact?.phone ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const submit = async (event: React.FormEvent) => {
    event.preventDefault(); setSaving(true); setError('')
    try {
      const response = await apiFetch(contact ? `/api/contacts/${contact.id}` : '/api/contacts', { method: contact ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, kind, taxId, email, phone }) })
      if (!response.ok) { const problem = await response.json().catch(() => null) as { detail?: string } | null; throw new Error(problem?.detail ?? 'Não foi possível salvar o contato.') }
      await onSaved()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Erro inesperado.'); setSaving(false) }
  }
  return <div className="modal-backdrop" onMouseDown={onClose}><form className="entry-modal contact-modal" onSubmit={submit} onMouseDown={event => event.stopPropagation()}><div className="modal-head"><div><p>Cadastro</p><h2>{contact ? 'Editar contato' : 'Novo contato'}</h2></div><button type="button" onClick={onClose}><X size={20} /></button></div><label>Nome ou razão social<input value={name} onChange={event => setName(event.target.value)} maxLength={100} autoFocus required /></label><label>Relacionamento<select value={kind} onChange={event => setKind(event.target.value as ContactData['kind'])}><option value="Customer">Cliente</option><option value="Supplier">Fornecedor</option><option value="Both">Cliente e fornecedor</option></select></label><div className="field-row"><label>CPF ou CNPJ<input value={taxId} onChange={event => setTaxId(event.target.value)} maxLength={20} placeholder="Opcional" /></label><label>Telefone<input value={phone} onChange={event => setPhone(event.target.value)} maxLength={30} placeholder="Opcional" /></label></div><label>E-mail<input type="email" value={email} onChange={event => setEmail(event.target.value)} maxLength={254} placeholder="Opcional" /></label>{error && <p className="form-error">{error}</p>}<div className="modal-actions"><button type="button" className="secondary-button" onClick={onClose}>Cancelar</button><button className="primary-button" disabled={saving || !name.trim()}>{saving ? 'Salvando…' : 'Salvar contato'}</button></div></form></div>
}

function AuditPage({ entries }: { entries: AuditData[] }) {
  const [query, setQuery] = useState('')
  const [entity, setEntity] = useState('all')
  const entities = [...new Set(entries.map(item => item.entityType))].sort()
  const visible = entries.filter(item => (entity === 'all' || item.entityType === entity) && `${item.description} ${item.action} ${item.entityType}`.toLowerCase().includes(query.trim().toLowerCase()))
  const actionLabels: Record<string, string> = { Created: 'Criação', Updated: 'Alteração', Voided: 'Estorno', Cancelled: 'Cancelamento', Settled: 'Baixa', Archived: 'Arquivamento', Activated: 'Reativação' }
  const entityLabels: Record<string, string> = { Transaction: 'Movimentação', Transfer: 'Transferência', Commitment: 'Compromisso', Contact: 'Contato' }
  return <div className="page audit-page"><section className="page-heading"><div><p className="eyebrow">CONTROLE</p><h1>Auditoria financeira</h1><p>Acompanhe alterações e estornos preservados no histórico do espaço.</p></div></section><section className="list-panel"><div className="list-toolbar audit-toolbar"><label className="search-box"><Search size={18} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Buscar no histórico" /></label><label className="filter-field"><History size={15} /><select value={entity} onChange={event => setEntity(event.target.value)}><option value="all">Todos os registros</option>{entities.map(item => <option value={item} key={item}>{entityLabels[item] ?? item}</option>)}</select></label></div><div className="audit-list">{visible.map(item => <article className="audit-row" key={item.id}><span className={`audit-action ${item.action.toLowerCase()}`}><History size={16} /></span><div><strong>{item.description}</strong><small>{entityLabels[item.entityType] ?? item.entityType} · {actionLabels[item.action] ?? item.action}</small></div><time dateTime={item.occurredAt}>{new Date(item.occurredAt).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' })}</time></article>)}{visible.length === 0 && <div className="contact-empty"><History size={27} /><strong>Nenhum evento encontrado</strong><span>As ações financeiras aparecerão aqui automaticamente.</span></div>}</div></section></div>
}

function ModulePage({ item, onBack }: { item: NavItem; onBack: () => void }) { const Icon = item.icon; const descriptions: Record<string, string> = { caixa: 'Acompanhe o realizado e antecipe como estará seu saldo nos próximos 30, 60 e 90 dias.', contas: 'Organize vencimentos, recebimentos e pendências sem perder nenhum prazo.', orcamentos: 'Defina limites por categoria e acompanhe os desvios durante o mês.', metas: 'Transforme objetivos financeiros em valores e prazos possíveis.', relatorios: 'Entenda receitas, despesas, resultado e evolução do seu negócio.' }; return <div className="page module-placeholder"><button className="back-link" onClick={onBack}><ChevronLeft size={16} /> Voltar para Hoje</button><div className="module-hero"><span><Icon size={26} /></span><p className="eyebrow">PRÓXIMO MÓDULO</p><h1>{item.label}</h1><p>{descriptions[item.id]}</p><div className="module-preview"><div /><div /><div /></div><small>A estrutura deste módulo já está reservada e será implementada na próxima etapa.</small></div></div> }

export default App
