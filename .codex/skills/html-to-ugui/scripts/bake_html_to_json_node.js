const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

async function main() {
  const [input, output, widthArg = '1920', heightArg = '1080', preview] = process.argv.slice(2);
  if (!input || !output) throw new Error('Usage: node bake_html_to_json_node.js input output [width] [height]');
  const width = Number(widthArg), height = Number(heightArg);
  const html = fs.readFileSync(input, 'utf8');
  const candidates = [
    process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH,
    'C:/Program Files/Google/Chrome/Application/chrome.exe',
    'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
    'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
    'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
  ].filter(Boolean);
  const executablePath = candidates.find(fs.existsSync);
  const browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) });
  const page = await browser.newPage({ viewport: { width: width + 100, height: height + 100 } });
  await page.setContent(`<style>body{margin:0}#sandbox{position:relative;width:${width}px;height:${height}px}#sandbox *{box-sizing:border-box!important}#sandbox [data-u-type]{min-width:0;min-height:0}</style><div id="sandbox"></div>`);
  await page.locator('#sandbox').evaluate((node, markup) => node.innerHTML = markup, html);
  await page.waitForTimeout(100);
  const result = await page.evaluate(({ width, height, sourcePath }) => {
    const root = document.querySelector('#sandbox [data-u-name]');
    if (!root) throw new Error('No UI-DSL root node found');
    const rootRect = root.getBoundingClientRect();
    const hex = value => {
      if (!value || value === 'transparent' || value === 'rgba(0, 0, 0, 0)') return '#FFFFFF00';
      const m = value.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)$/);
      if (!m) return '#FFFFFF';
      const h = n => Number(n).toString(16).padStart(2, '0');
      const a = m[4] == null ? '' : h(Math.round(Number(m[4]) * 255));
      return `#${h(m[1])}${h(m[2])}${h(m[3])}${a}`;
    };
    const attr = (el, names) => names.map(n => el.getAttribute(n)).find(Boolean) || '';
    const layout = (el, rect, parent) => {
      const explicit = attr(el, ['data-u-layout', 'data-u-anchor']);
      if (explicit) return explicit;
      const l = rect.left - parent.left, t = rect.top - parent.top, r = parent.right - rect.right, b = parent.bottom - rect.bottom;
      const tol = Math.max(2, Math.min(parent.width, parent.height) * .01);
      if (l <= tol && t <= tol && r <= tol && b <= tol) return 'stretch';
      if (t <= tol && l <= tol && r <= tol) return 'top-bar';
      if (b <= tol && l <= tol && r <= tol) return 'bottom-bar';
      if (l <= tol && t <= tol && b <= tol) return 'left-panel';
      if (r <= tol && t <= tol && b <= tol) return 'right-panel';
      return 'fixed';
    };
    const visit = (el, parentRect) => {
      const type = el.getAttribute('data-u-type'), name = el.getAttribute('data-u-name');
      const children = [...el.children].map(child => visit(child, name && type ? el.getBoundingClientRect() : parentRect)).filter(Boolean);
      if (!type || !name) return children.length === 1 ? children[0] : null;
      const rect = el.getBoundingClientRect(), style = getComputedStyle(el);
      const bg = style.backgroundImage.match(/url\(["']?(.*?)["']?\)/);
      const node = { schemaVersion:2, name, type, dir:el.getAttribute('data-u-dir')||'v', value:Number(el.getAttribute('data-u-value'))||.5,
        isChecked:el.getAttribute('data-u-checked')==='true', options:[], x:Math.round(rect.left-rootRect.left), y:Math.round(rect.top-rootRect.top),
        width:Math.round(rect.width), height:Math.round(rect.height), color:hex(style.backgroundColor), fontColor:hex(style.color),
        fontSize:Math.round(parseFloat(style.fontSize)||14), textAlign:style.textAlign||'center', text:(el.innerText||'').trim(),
        imageSrc:attr(el,['data-u-src','src']), backgroundImageSrc:bg?bg[1]:'', imageFit:attr(el,['data-u-fit'])||style.objectFit||style.backgroundSize||'',
        layoutHint:layout(el,rect,parentRect), safeArea:attr(el,['data-u-safe-area']), anchorPreset:attr(el,['data-u-anchor']),
        anchorMin:attr(el,['data-u-anchor-min']), anchorMax:attr(el,['data-u-anchor-max']), pivot:attr(el,['data-u-pivot']),
        offsetMin:attr(el,['data-u-offset-min']), offsetMax:attr(el,['data-u-offset-max']), children };
      return node;
    };
    const baked = visit(root, document.querySelector('#sandbox').getBoundingClientRect());
    baked.designWidth=width; baked.designHeight=height; baked.sourcePath=sourcePath; baked.htmlFilePath=sourcePath; baked.sourceDirectory=sourcePath.replace(/[\\/][^\\/]+$/, '');
    return baked;
  }, { width, height, sourcePath: path.resolve(input) });
  if (preview) {
    await page.locator('[data-u-name="m_go_RewardPanel"]').evaluate(node => node.style.display = 'none');
    await page.screenshot({ path: preview, clip: { x: 0, y: 0, width, height } });
  }
  await browser.close();
  fs.writeFileSync(output, JSON.stringify(result, null, 2), 'utf8');
  console.log(`Bake complete: ${output}`);
}
main().catch(error => { console.error(error); process.exit(1); });
