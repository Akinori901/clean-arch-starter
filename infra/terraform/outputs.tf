output "cognito_user_pool_id" {
  value = aws_cognito_user_pool.main.id
}

output "cognito_client_id" {
  value = aws_cognito_user_pool_client.web.id
}

output "ecr_django_url" {
  value = aws_ecr_repository.django.repository_url
}

output "ecr_laravel_url" {
  value = aws_ecr_repository.laravel.repository_url
}
