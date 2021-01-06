substring-before(//actor[@id="1"]/text(), "n")

--url "http://se-fan.ru/pictures/ru/2/128x128/erogirls/all.xhtml" --link "//a[text()=\"Оригинал\"]" --output "C:\proj\download" --container "//a[contains(@title,\"Смотреть\")]" --nextLinks "//a[@title=\"Далее\"]" --names "//h1" --nextContainer "//a[@title=\"Далее\"]"

links //div[@class="image"]/a | //div[@class="image"]/img
next //a[@class="next"]
containers //div[preceding-sibling::h2/text()="Читает"]/a