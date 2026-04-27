import asyncio
from playwright.async_api import async_playwright

async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch()
        page = await browser.new_page()
        await page.goto('http://localhost:4200')
        await asyncio.sleep(2)  # Wait for Angular to initialize
        await page.screenshot(path='frontend_screenshot.png')
        await browser.close()

asyncio.run(main())
