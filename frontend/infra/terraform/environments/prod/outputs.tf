output "bucket_name" {
  description = "Nome do bucket S3 do frontend de prod — informativo, bate com BUCKET_NAME no GitHub Environment prod."
  value       = aws_s3_bucket.frontend.id
}

output "bucket_arn" {
  description = "ARN do bucket S3 do frontend de prod."
  value       = aws_s3_bucket.frontend.arn
}
