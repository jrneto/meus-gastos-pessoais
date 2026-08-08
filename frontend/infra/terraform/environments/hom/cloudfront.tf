resource "aws_cloudfront_origin_access_control" "frontend" {
  name                              = "oac-gastosapp-frontend-hom"
  description                       = "OAC do bucket de homologacao do frontend"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_cloudfront_distribution" "main" {
  enabled             = true
  aliases             = [var.hom_domain_name]
  default_root_object = "index.html"
  price_class         = "PriceClass_All" # igual produção — sem diferença de custo dentro do plano Free (ver plan.md)
  http_version        = "http2"
  is_ipv6_enabled     = true

  origin {
    domain_name              = aws_s3_bucket.frontend.bucket_regional_domain_name
    origin_id                = "gastosapp-frontend-hom.s3.${var.aws_region}.amazonaws.com"
    origin_access_control_id = aws_cloudfront_origin_access_control.frontend.id
  }

  default_cache_behavior {
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "gastosapp-frontend-hom.s3.${var.aws_region}.amazonaws.com"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    # Cache policy gerenciada pela AWS ("CachingOptimized") — mesma já
    # usada em environments/prod/cloudfront.tf.
    cache_policy_id = "658327ea-f89d-4fab-a63d-7e88639e58f6"
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate.hom.arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = "TLSv1.2_2021"
  }

  web_acl_id = aws_wafv2_web_acl.hom.arn

  tags = {
    Name = "gastosapp-cdn-hom"
  }
}
