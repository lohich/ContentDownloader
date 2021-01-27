--url "http://se-fan.ru/pictures/ru/2/128x128/erogirls/all.xhtml" --link "//a[text()=\"Оригинал\"]" --output "C:\proj\download" --container "//a[contains(@title,\"Смотреть\")]" --nextLinks "//a[@title=\"Далее\"]" --names "//h1" --nextContainer "//a[@title=\"Далее\"]"

--url "http://joyreactor.cc/user/lohich/favorite" --link "//div[@class=\"image\"]/a | //div[@class=\"image\"]/img" --output "C:\proj\download" --nextContainer "//a[@class=\"next\"]"

--url "http://joyreactor.cc/user/lohich" --link "//div[@class=\"image\"]/a | //div[@class=\"image\"]/img" --output "C:\proj\download" --nextContainer "//a[@class=\"next\"]" --nextLinks "//a[@class=\"next\"]" --container "//div[preceding-sibling::h2/text()=\"Читает\"]/a"

--url "http://joyreactor.cc/user/lohich" --link "//div[@class=\"image\"]/a | //div[@class=\"image\"]/img" --output "C:\proj\download" --nextContainer "//a[@class=\"next\"]" --nextLinks "//a[@class=\"next\"]" --container "//div[preceding-sibling::h2/text()=\"Читает\"]/a;//a[text()='Бездна']" --authUrl http://joyreactor.cc/login --authLogin "//div[@id='content']//input[@name='signin[username]'];lohich2" --authPassword "//div[@id='content']//input[@name='signin[password]'];478033WoW" --authConfirm "//div[@id='content']//input[@type='submit']"

links //div[@class="image"]/a | //div[@class="image"]/img
next //a[@class="next"]
containers //div[preceding-sibling::h2/text()="Читает"]/a