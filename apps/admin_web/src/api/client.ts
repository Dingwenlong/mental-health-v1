export type ApiProblem = {
  type?: string
  title?: string
  detail?: string
  status?: number
  code: string
  traceId?: string
  [key: string]: unknown
}

export class ApiProblemError extends Error {
  readonly problem: ApiProblem
  readonly retryAfterSeconds: number | null

  constructor(problem: ApiProblem, retryAfterSeconds: number | null = null) {
    super(problem.detail ?? problem.title ?? problem.code)
    this.name = 'ApiProblemError'
    this.problem = problem
    this.retryAfterSeconds = retryAfterSeconds
  }
}

const accessTokenKey = 'mh_access_token'

export const tokenStore = {
  read: (): string | null => sessionStorage.getItem(accessTokenKey),
  write: (token: string): void => sessionStorage.setItem(accessTokenKey, token),
  clear: (): void => sessionStorage.removeItem(accessTokenKey),
}

export class ApiClient {
  private readonly baseUrl: string
  private readonly readToken: () => string | null

  constructor(
    baseUrl: string,
    readToken: () => string | null = tokenStore.read,
  ) {
    this.baseUrl = baseUrl
    this.readToken = readToken
  }

  get<T>(path: string): Promise<T> {
    return this.request<T>('GET', path)
  }

  post<T>(path: string, body?: unknown, bearerToken?: string): Promise<T> {
    return this.request<T>('POST', path, body, bearerToken)
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>('PUT', path, body)
  }

  delete(path: string): Promise<void> {
    return this.request<void>('DELETE', path)
  }

  private async request<T>(
    method: string,
    path: string,
    body?: unknown,
    bearerToken?: string,
  ): Promise<T> {
    const token = bearerToken ?? this.readToken()
    const response = await fetch(new URL(path, this.normalizedBaseUrl()), {
      method,
      headers: {
        Accept: 'application/json',
        ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    })

    if (!response.ok) {
      const problem = (await this.readJson(response)) as Partial<ApiProblem>
      throw new ApiProblemError({
        ...problem,
        code: typeof problem.code === 'string' ? problem.code : 'HTTP_ERROR',
        status: problem.status ?? response.status,
      }, this.readRetryAfterSeconds(response))
    }
    if (response.status === 204) {
      return undefined as T
    }
    return (await response.json()) as T
  }

  private normalizedBaseUrl(): URL {
    return new URL(this.baseUrl.endsWith('/') ? this.baseUrl : `${this.baseUrl}/`)
  }

  private async readJson(response: Response): Promise<Record<string, unknown>> {
    try {
      return (await response.json()) as Record<string, unknown>
    } catch {
      return {}
    }
  }

  private readRetryAfterSeconds(response: Response): number | null {
    const value = response.headers.get('Retry-After')
    return value !== null && /^\d+$/.test(value) ? Number(value) : null
  }
}

export const apiClient = new ApiClient(
  import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5165/api/v1/',
)
