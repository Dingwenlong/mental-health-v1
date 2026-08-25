export type CaptchaBootstrap = {
  preChallengeToken: string
  prefix: string
  encryptedSceneId: string
  expiresAt: string
}

type AliyunCaptchaInstance = { startTracelessVerification: () => void }

type AliyunCaptchaOptions = {
  SceneId: string
  prefix: string
  mode: 'popup'
  language: 'cn'
  delayBeforeSuccess: false
  slideStyle: { width: number; height: number }
  EncryptedSceneId: string
  captchaVerifyCallback: (captchaVerifyParam: string) => Promise<{ captchaResult: boolean; bizResult: boolean }>
  onError: (error: unknown) => void
}

declare global {
  interface Window {
    AliyunCaptchaConfig?: { region: 'cn'; prefix: string }
    initAliyunCaptcha?: (options: AliyunCaptchaOptions, ready: (captcha: AliyunCaptchaInstance) => void) => void
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
    window.initAliyunCaptcha?.(
      {
        SceneId: '1lae8yfm',
        prefix: bootstrap.prefix,
        EncryptedSceneId: bootstrap.encryptedSceneId,
        mode: 'popup',
        language: 'cn',
        delayBeforeSuccess: false,
        slideStyle: { width: 360, height: 40 },
        captchaVerifyCallback: async (captchaVerifyParam) => {
          resolve(captchaVerifyParam)
          return { captchaResult: true, bizResult: true }
        },
        onError: reject,
      },
      (captcha) => captcha.startTracelessVerification(),
    )
  })
}
