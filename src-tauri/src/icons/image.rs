//! Icon bytes from anywhere become a clean, size-capped PNG. Raster formats are fully decoded and
//! re-encoded (no original bytes are passed through); SVG is rendered with every external
//! reference disabled. Remote fetch guards live here too.

use crate::error::{AppError, AppResult};
use image::{imageops::FilterType, ImageFormat, ImageReader, Limits};
use std::io::Cursor;
use std::net::IpAddr;

const MAX_PIXELS_SIDE: u32 = 4096;
const MAX_SVG_USES: usize = 64;

fn bad(msg: impl Into<String>) -> AppError {
    AppError::new("parse", msg)
}

fn looks_like_svg(bytes: &[u8]) -> bool {
    let head = String::from_utf8_lossy(&bytes[..bytes.len().min(1024)]).to_ascii_lowercase();
    head.contains("<svg")
}

fn render_svg(bytes: &[u8], max: u32) -> AppResult<Vec<u8>> {
    use resvg::usvg;
    // <use> fan-out can expand exponentially while building the render tree; real icons use few.
    let text = String::from_utf8_lossy(bytes).to_ascii_lowercase();
    if text.matches("<use").count() > MAX_SVG_USES {
        return Err(bad("svg: too complex"));
    }
    let opt = usvg::Options {
        image_href_resolver: usvg::ImageHrefResolver {
            resolve_data: Box::new(|_, _, _| None),
            resolve_string: Box::new(|_, _| None),
        },
        ..usvg::Options::default()
    };
    let tree = usvg::Tree::from_data(bytes, &opt).map_err(|e| bad(format!("svg: {e}")))?;
    let size = tree.size();
    let scale = max as f32 / size.width().max(size.height()).max(1.0);
    let (w, h) = (
        ((size.width() * scale).round() as u32).clamp(1, max),
        ((size.height() * scale).round() as u32).clamp(1, max),
    );
    let mut pixmap = resvg::tiny_skia::Pixmap::new(w, h).ok_or_else(|| bad("svg: empty canvas"))?;
    resvg::render(
        &tree,
        resvg::tiny_skia::Transform::from_scale(scale, scale),
        &mut pixmap.as_mut(),
    );
    pixmap
        .encode_png()
        .map_err(|e| bad(format!("svg encode: {e}")))
}

/// Decode any supported image and re-encode it as PNG no larger than `max` x `max`.
pub fn to_png(bytes: &[u8], max: u32) -> AppResult<Vec<u8>> {
    if bytes.is_empty() {
        return Err(bad("empty image"));
    }
    if looks_like_svg(bytes) {
        return render_svg(bytes, max);
    }
    let mut reader = ImageReader::new(Cursor::new(bytes))
        .with_guessed_format()
        .map_err(|e| bad(e.to_string()))?;
    let mut limits = Limits::default();
    limits.max_image_width = Some(MAX_PIXELS_SIDE);
    limits.max_image_height = Some(MAX_PIXELS_SIDE);
    limits.max_alloc = Some(64 * 1024 * 1024);
    reader.limits(limits);
    let img = reader.decode().map_err(|e| bad(format!("image: {e}")))?;
    let img = if img.width() > max || img.height() > max {
        img.resize(max, max, FilterType::Lanczos3)
    } else {
        img
    };
    let mut out = Cursor::new(Vec::new());
    img.write_to(&mut out, ImageFormat::Png)
        .map_err(|e| bad(e.to_string()))?;
    Ok(out.into_inner())
}

/// Accumulates a download and refuses to grow past a fixed size.
pub struct CappedBuf {
    cap: usize,
    pub data: Vec<u8>,
}

impl CappedBuf {
    pub fn new(cap: usize) -> Self {
        Self {
            cap,
            data: Vec::new(),
        }
    }

    pub fn push(&mut self, chunk: &[u8]) -> AppResult<()> {
        if self.data.len() + chunk.len() > self.cap {
            return Err(AppError::new("network", "response too large"));
        }
        self.data.extend_from_slice(chunk);
        Ok(())
    }
}

/// Remote icon hosts must resolve only to public internet addresses.
pub fn is_public_ip(ip: IpAddr) -> bool {
    match ip {
        IpAddr::V4(v4) => {
            let o = v4.octets();
            !(v4.is_loopback()
                || v4.is_private()
                || v4.is_link_local()
                || v4.is_broadcast()
                || v4.is_documentation()
                || v4.is_unspecified()
                || (o[0] == 100 && (o[1] & 0xC0) == 64))
        }
        IpAddr::V6(v6) => {
            if let Some(v4) = v6.to_ipv4_mapped() {
                return is_public_ip(IpAddr::V4(v4));
            }
            let s = v6.segments();
            !(v6.is_loopback()
                || v6.is_unspecified()
                || (s[0] & 0xfe00) == 0xfc00
                || (s[0] & 0xffc0) == 0xfe80)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use image::{ImageFormat, Rgba, RgbaImage};
    use std::io::Cursor;

    fn sample(format: ImageFormat, size: u32) -> Vec<u8> {
        let img = RgbaImage::from_fn(size, size, |x, y| {
            Rgba([(x * 7) as u8, (y * 5) as u8, 180, 255])
        });
        let mut out = Cursor::new(Vec::new());
        image::DynamicImage::ImageRgba8(img)
            .write_to(&mut out, format)
            .unwrap();
        out.into_inner()
    }

    fn dims(png: &[u8]) -> (u32, u32) {
        let img = image::load_from_memory_with_format(png, ImageFormat::Png).unwrap();
        (img.width(), img.height())
    }

    #[test]
    fn raster_formats_become_png_capped_at_max() {
        for f in [
            ImageFormat::Png,
            ImageFormat::Ico,
            ImageFormat::Jpeg,
            ImageFormat::Bmp,
        ] {
            let src = sample(f, if f == ImageFormat::Ico { 64 } else { 256 });
            let png = to_png(&src, 128).unwrap_or_else(|e| panic!("{f:?}: {e}"));
            let (w, h) = dims(&png);
            assert!(w <= 128 && h <= 128 && w >= 32, "{f:?}: {w}x{h}");
        }
    }

    #[test]
    fn small_images_are_not_upscaled() {
        let png = to_png(&sample(ImageFormat::Png, 48), 128).unwrap();
        assert_eq!(dims(&png), (48, 48));
    }

    #[test]
    fn svg_renders_and_ignores_external_references() {
        let svg = br##"<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 10 10">
            <rect width="10" height="10" fill="#1f5c45"/>
            <image xlink:href="file:///C:/Windows/win.ini" width="10" height="10"/>
            <image xlink:href="https://evil.example/x.png" width="10" height="10"/>
        </svg>"##;
        let png = to_png(svg, 128).unwrap();
        assert_eq!(dims(&png), (128, 128));
    }

    #[test]
    fn broken_input_is_an_error_not_a_panic() {
        let full = sample(ImageFormat::Png, 64);
        assert!(to_png(&full[..full.len() / 3], 128).is_err());
        assert!(to_png(b"<html>not an image</html>", 128).is_err());
        assert!(to_png(&[], 128).is_err());
    }

    #[test]
    fn capped_buffer_stops_at_the_limit() {
        let mut b = CappedBuf::new(10);
        assert!(b.push(&[0; 6]).is_ok());
        assert!(b.push(&[0; 4]).is_ok());
        assert!(b.push(&[0; 1]).is_err());
    }

    #[test]
    fn only_public_addresses_are_allowed() {
        for ip in [
            "127.0.0.1",
            "10.1.2.3",
            "192.168.1.1",
            "172.16.0.1",
            "169.254.1.1",
            "100.64.0.1",
            "0.0.0.0",
            "::1",
            "fc00::1",
            "fe80::1",
            "::ffff:127.0.0.1",
        ] {
            assert!(!is_public_ip(ip.parse().unwrap()), "{ip}");
        }
        for ip in ["1.1.1.1", "140.82.112.3", "2606:4700::1111"] {
            assert!(is_public_ip(ip.parse().unwrap()), "{ip}");
        }
    }
}

#[cfg(test)]
mod bombs {
    use super::*;

    /// 10 levels of 10-way <use> fan-out (10^10 nodes if expanded naively) must not hang or
    /// exhaust memory in the elevated process.
    #[test]
    fn svg_use_fan_out_is_refused_quickly() {
        let mut svg = String::from(
            r#"<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 10 10"><defs><rect id="l0" width="1" height="1"/>"#,
        );
        for level in 1..=10 {
            svg.push_str(&format!(r#"<g id="l{level}">"#));
            for _ in 0..10 {
                svg.push_str(&format!(r##"<use xlink:href="#l{}"/>"##, level - 1));
            }
            svg.push_str("</g>");
        }
        svg.push_str(r##"</defs><use xlink:href="#l10"/></svg>"##);
        let t = std::time::Instant::now();
        let _ = to_png(svg.as_bytes(), 128);
        assert!(
            t.elapsed() < std::time::Duration::from_secs(2),
            "took {:?}",
            t.elapsed()
        );
    }
}
