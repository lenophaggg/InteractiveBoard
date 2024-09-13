// Массив для хранения ссылок на SVG файлы
const svgFiles = {
    1: '/img/FloorPlans/1fl.svg', // Укажите относительный путь от wwwroot
    2: '/img/FloorPlans/2fl.svg',
    3: '/img/FloorPlans/3fl.svg',
    4: '/img/FloorPlans/4fl.svg',
    5: '/img/FloorPlans/5fl.svg'
};

// Функция для смены этажа
function changeFloor(floorNumber) {
    // Обновляем заголовок этажа
    document.getElementById("floor-title").innerText = floorNumber + " этаж";

    // Загружаем соответствующий SVG файл
    fetch(svgFiles[floorNumber])
        .then(response => response.text())
        .then(svgContent => {
            // Обновляем содержимое SVG
            document.getElementById("svg-container").innerHTML = svgContent;
        })
        .catch(error => console.error('Ошибка при загрузке SVG:', error));
}

// По умолчанию загружаем 1 этаж
changeFloor(1);
