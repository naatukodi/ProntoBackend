using System.Net;
using Newtonsoft.Json;

namespace Valuation.Api.Services
{
    /// <summary>
    /// The standalone photo gallery a report's IMAGE LINK opens.
    ///
    /// A copy of BuildGalleryHtml in ProntoPDFGeneration (PdfReportService.cs), which
    /// publishes the same page beside every portal report, so a report-builder report's
    /// gallery cannot be told apart from a portal one. Change the two together.
    /// </summary>
    public static class ReportGalleryPage
    {
        public static string Build(string registrationNumber, IEnumerable<(string Label, string Url)> photos)
        {
            // The builder sends this; unlike the PDF service's, it is not from our own data.
            var regNo = WebUtility.HtmlEncode(registrationNumber.ToUpperInvariant());
            var json  = JsonConvert.SerializeObject(photos.Select(p => new { n = p.Label, u = p.Url }),
                new JsonSerializerSettings { StringEscapeHandling = StringEscapeHandling.EscapeHtml });

            return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{regNo}} — Photo Gallery</title>
<style>
  * { margin:0; padding:0; box-sizing:border-box; }
  body { background:#0f172a; color:#fff; font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;
         height:100dvh; display:flex; flex-direction:column; overflow:hidden; }
  header { padding:10px 14px; display:flex; align-items:center; gap:10px; background:#111c33; }
  header .reg   { font-weight:700; font-size:14px; letter-spacing:.5px; color:#5eead4; white-space:nowrap; }
  header .name  { font-weight:700; font-size:13px; text-align:center; flex:1; }
  header .count { font-size:12px; color:#94a3b8; white-space:nowrap; }
  .stage { flex:1; position:relative; display:flex; align-items:center; justify-content:center; min-height:0; }
  .stage img { max-width:100%; max-height:100%; object-fit:contain; }
  .nav { position:absolute; top:50%; transform:translateY(-50%); width:44px; height:44px; border-radius:50%;
         border:none; background:rgba(255,255,255,.12); color:#fff; font-size:22px; cursor:pointer; }
  .nav:active { background:rgba(255,255,255,.3); }
  .prev { left:10px; } .next { right:10px; }
  .thumbs { display:flex; gap:6px; overflow-x:auto; padding:8px 10px; background:#111c33; }
  .thumbs img { width:64px; height:48px; object-fit:cover; border-radius:6px; opacity:.5; cursor:pointer;
                flex:0 0 auto; border:2px solid transparent; }
  .thumbs img.active { opacity:1; border-color:#5eead4; }
</style>
</head>
<body>
<header><span class="reg">{{regNo}}</span><span class="name" id="name"></span><span class="count" id="count"></span></header>
<div class="stage">
  <img id="main" alt="">
  <button class="nav prev" onclick="go(-1)">&#10094;</button>
  <button class="nav next" onclick="go(1)">&#10095;</button>
</div>
<div class="thumbs" id="thumbs"></div>
<script>
const photos = {{json}};
let i = 0;
const main = document.getElementById('main'), nameEl = document.getElementById('name'),
      countEl = document.getElementById('count'), thumbs = document.getElementById('thumbs');
photos.forEach((p, idx) => {
  const t = document.createElement('img');
  t.src = p.u; t.loading = 'lazy'; t.alt = p.n;
  t.onclick = () => show(idx);
  thumbs.appendChild(t);
});
function show(n) {
  i = (n + photos.length) % photos.length;
  main.src = photos[i].u;
  nameEl.textContent = photos[i].n;
  countEl.textContent = (i + 1) + ' / ' + photos.length;
  [...thumbs.children].forEach((t, idx) => t.classList.toggle('active', idx === i));
  thumbs.children[i].scrollIntoView({ inline:'center', block:'nearest', behavior:'smooth' });
  if (photos[i + 1]) new Image().src = photos[i + 1].u;
}
function go(d) { show(i + d); }
document.addEventListener('keydown', e => {
  if (e.key === 'ArrowLeft') go(-1);
  if (e.key === 'ArrowRight') go(1);
});
let sx = null;
const stage = document.querySelector('.stage');
stage.addEventListener('touchstart', e => sx = e.touches[0].clientX, { passive:true });
stage.addEventListener('touchend', e => {
  if (sx === null) return;
  const dx = e.changedTouches[0].clientX - sx;
  if (Math.abs(dx) > 40) go(dx < 0 ? 1 : -1);
  sx = null;
}, { passive:true });
show(0);
</script>
</body>
</html>
""";
        }
    }
}
