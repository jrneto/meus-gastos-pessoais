# Nome/descrição auto-gerados pelo console na criação da distribuição —
# mantidos como estão para o terraform plan bater "No changes".
resource "aws_cloudfront_origin_access_control" "frontend" {
  name                              = "oac-gastosapp-frontend-prod.s3.us-east-1.amazonaws.c-ms7fs2wuxhv"
  description                       = "Created by CloudFront"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_cloudfront_distribution" "main" {
  enabled             = true
  aliases             = [var.domain_name, "www.${var.domain_name}"]
  default_root_object = "index.html"
  price_class         = "PriceClass_All"
  http_version        = "http2"
  is_ipv6_enabled     = true

  # origin_id gerado automaticamente pelo console na criação da
  # distribuição — mantido como está (é só um identificador interno de
  # ligação entre origin e cache behavior, sem significado além disso).
  origin {
    domain_name              = aws_s3_bucket.frontend.bucket_regional_domain_name
    origin_id                = "gastosapp-frontend-prod.s3.us-east-1.amazonaws.com-ms7f8nrkx2n"
    origin_access_control_id = aws_cloudfront_origin_access_control.frontend.id
  }

  default_cache_behavior {
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "gastosapp-frontend-prod.s3.us-east-1.amazonaws.com-ms7f8nrkx2n"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    # Cache policy gerenciada pela AWS ("CachingOptimized") — não é um
    # recurso deste projeto, referenciada por ID fixo.
    cache_policy_id = "658327ea-f89d-4fab-a63d-7e88639e58f6"
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  # SPA (React Router, client-side routing): sem isso, dar F5 em
  # qualquer rota que não seja "/" pede esse path literal ao S3, que
  # não tem esse objeto e responde 403 (bucket privado via OAC) — o
  # CloudFront repassava esse XML direto pro navegador. Redireciona
  # 403/404 pro index.html (200), deixando o React Router decidir a
  # rota no client.
  custom_error_response {
    error_code         = 403
    response_code      = 200
    response_page_path = "/index.html"
  }

  custom_error_response {
    error_code         = 404
    response_code      = 200
    response_page_path = "/index.html"
  }

  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate.frontend.arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = "TLSv1.2_2021" # confirmado via aws cloudfront get-distribution-config
  }

  web_acl_id = aws_wafv2_web_acl.frontend.arn

  tags = {
    Name = "gastosapp-cdn"
  }
}