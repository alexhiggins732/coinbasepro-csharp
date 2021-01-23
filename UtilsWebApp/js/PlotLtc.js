var graphDiv = null;
Plotly.d3.json("js/ltcusd-hour24.js", function (err, rows) {

    function unpack(rows, key) {
        return rows.map(function (row) { return row[key]; });
    }


    
    var trace1 = {
        type: "scatter",
        mode: "lines",
        name: 'High',
        x: [],
        y: [],
        line: { color: '#17BECF' }
    }

    var trace2 = {
        type: "scatter",
        mode: "lines",
        name: 'Low',
        x: [],
        y: [],
        line: { color: '#7F7F7F' }
    }
    var trace3 = {
        type: "scatter",
        mode: "lines",
        name: 'Close',
        x: [],
        y: [],
        line: { color: '#7F7F7F' }
    }

    for (var i = 0; i < rows.length; i++) {
        var candle = rows[i];
        trace1.x.push(candle.Time);
        trace2.x.push(candle.Time);
        trace3.x.push(candle.Time);
        trace1.y.push(candle.High);
        trace2.y.push(candle.Low);
        trace3.y.push(candle.Close);
    }
    var data = [trace1, trace2, trace3];

    var layout = {
        title: 'Time Series with Rangeslider',
        xaxis: {
            autorange: true,
            range: ['2015-02-17', '2017-02-16'],
            rangeselector: {
                buttons: [
                    {
                        count: 1,
                        label: '1m',
                        step: 'month',
                        stepmode: 'backward'
                    },
                    {
                        count: 6,
                        label: '6m',
                        step: 'month',
                        stepmode: 'backward'
                    },
                    { step: 'all' }
                ]
            },
            rangeslider: { range: ['2015-02-17', '2017-02-16'] },
            type: 'date'
        },
        yaxis: {
            autorange: true,
            range: [86.8700008333, 138.870004167],
            type: 'linear'
        }
    };

    Plotly.newPlot('myDiv', data, layout);
    graphDiv = document.getElementById('myDiv');
    bindlayout(graphDiv.layout);
})
