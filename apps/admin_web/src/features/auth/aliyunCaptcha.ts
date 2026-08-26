export type CaptchaBootstrap = {
  preChallengeToken: string
  prefix: string
  encryptedSceneId: string
  expiresAt: string
}

type AliyunCaptchaInstance = { startTracelessVerification: () => void }

type AliyunCaptchaOptions = {
  SceneId: string
  mode: 'popup'
  element: '#admin-captcha-element'
  button: '#admin-captcha-button'
  language: 'cn'
  delayBeforeSuccess: false
  slideStyle: { width: number; height: number }
  EncryptedSceneId: string
  success: (captchaVerifyParam: string) => void
  fail: () => void
  getInstance: (captcha: AliyunCaptchaInstance) => void
  onError: (error: unknown) => void
  onClose: (reason: string) => void
}

declare global {
  interface Window {
    AliyunCaptchaConfig?: { region: 'cn'; prefix: string }
    initAliyunCaptcha?: (options: AliyunCaptchaOptions) => void
  }
}

const scriptUrl = 'https://o.alicdn.com/captcha-frontend/aliyunCaptcha/AliyunCaptcha.js'
let scriptLoad: Promise<void> | null = null

function loadScript(prefix: string): Promise<void> {
  if (scriptLoad) return scriptLoad
  window.AliyunCaptchaConfig = { region: 'cn', prefix }
  const load = new Promise<void>((resolve, reject) => {
    const script = document.createElement('script')
    script.src = scriptUrl
    script.async = true
    script.onload = () => resolve()
    script.onerror = () => {
      script.remove()
      if (scriptLoad === load) scriptLoad = null
      reject(new Error('Aliyun captcha script failed to load'))
    }
    document.head.append(script)
  })
  scriptLoad = load
  return load
}

export async function runAliyunCaptcha(bootstrap: CaptchaBootstrap): Promise<string> {
  await loadScript(bootstrap.prefix)
  if (!window.initAliyunCaptcha) throw new Error('Aliyun captcha is unavailable')

  return new Promise<string>((resolve, reject) => {
    let settled = false
    const succeed = (captchaVerifyParam: string): void => {
      if (settled) return
      settled = true
      resolve(captchaVerifyParam)
    }
    const fail = (message: string, cause?: unknown): void => {
      if (settled) return
      settled = true
      reject(cause instanceof Error ? cause : new Error(message))
    }

    window.initAliyunCaptcha?.(
      {
        SceneId: '1lae8yfm',
        EncryptedSceneId: bootstrap.encryptedSceneId,
        mode: 'popup',
        element: '#admin-captcha-element',
        button: '#admin-captcha-button',
        language: 'cn',
        delayBeforeSuccess: false,
        slideStyle: { width: 360, height: 40 },
        success: succeed,
        fail: () => undefined,
        getInstance: (captcha) => {
          window.setTimeout(() => {
            if (settled) return
            if (!captcha || typeof captcha.startTracelessVerification !== 'function') {
              fail('Aliyun captcha instance is unavailable')
              return
            }
            try { captcha.startTracelessVerification() }
            catch (error) { fail('Aliyun captcha failed to start', error) }
          }, 2100)
        },
        onError: (error) => fail('Aliyun captcha failed to initialize', error),
        onClose: (reason) => {
          if (reason === 'userDismiss') fail('Aliyun captcha was closed')
        },
      },
    )
  })
}
