output "bucket_name" {
  description = "Nome do bucket S3 do frontend de hom — informativo, bate com BUCKET_NAME no GitHub Environment hom."
  value       = aws_s3_bucket.frontend.id
}

output "bucket_arn" {
  description = "ARN do bucket S3 do frontend de hom."
  value       = aws_s3_bucket.frontend.arn
}
