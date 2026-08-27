variable "project" {
  type        = string
  default     = "clean-arch-starter"
  description = "リソース名の接頭辞"
}

variable "env" {
  type        = string
  description = "環境名（dev / stg / prod）"
  validation {
    condition     = contains(["dev", "stg", "prod"], var.env)
    error_message = "env は dev / stg / prod のいずれかにしてください。"
  }
}

variable "region" {
  type    = string
  default = "ap-northeast-1"
}

variable "callback_urls" {
  type        = list(string)
  description = "Cognito のコールバック URL（CloudFront のドメイン）"
  default     = []
}
