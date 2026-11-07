// Positive cubic B-spline reconstruction avoids visible half-resolution texel edges.
// Four bilinear fetches; no larger cloud allocation or denser volume tracing.
fn forgeCloudViewFiltered( uv: vec2f ) -> vec4f {
 let size = vec2f( textureDimensions( cloudsView ) );
 let pixel = uv * size - 0.5;
 let base = floor( pixel ); let f = fract( pixel ); let inv = 1.0 - f;
 let w0 = inv * inv * inv / 6.0;
 let w1 = ( 3.0 * f * f * f - 6.0 * f * f + 4.0 ) / 6.0;
 let w2 = ( -3.0 * f * f * f + 3.0 * f * f + 3.0 * f + 1.0 ) / 6.0;
 let w3 = f * f * f / 6.0;
 let g0 = w0 + w1; let g1 = w2 + w3;
 let p0 = ( base - 1.0 + w1 / g0 + 0.5 ) / size;
 let p1 = ( base + 1.0 + w3 / g1 + 0.5 ) / size;
 return textureSampleLevel( cloudsView, smpLinearClamp, p0, 0.0 ) * g0.x * g0.y
      + textureSampleLevel( cloudsView, smpLinearClamp, vec2f( p1.x, p0.y ), 0.0 ) * g1.x * g0.y
      + textureSampleLevel( cloudsView, smpLinearClamp, vec2f( p0.x, p1.y ), 0.0 ) * g0.x * g1.y
      + textureSampleLevel( cloudsView, smpLinearClamp, p1, 0.0 ) * g1.x * g1.y;
}
