"""Reusable top-down station symbols; tableware uses one dish family."""
def icon(kind):
    if kind == 'dishes': kind = 'bowls'
    # Native diagram symbols, centered at (0,0); no generated raster art.
    if kind=='tomato':return '<ellipse cy="1" rx="19" ry="16" fill="#D65D46"/><path d="M-9 -13 0 -17 9 -12 2 -11 0 -5 -3 -11Z" fill="#527B46"/>'
    if kind=='cucumber':return '<rect x="-24" y="-11" width="48" height="22" rx="11" fill="#517744" transform="rotate(-23)"/><path d="M-17 5 16 -9" stroke="#A9BE74" stroke-width="3" stroke-linecap="round"/>'
    if kind=='carrot':return '<path d="M-11 -14Q0 -22 12 -12L0 23Q-3 25 -5 17Z" fill="#E3933E"/><path d="M-1 -16 -10 -26 M1 -17 9 -26" stroke="#64884C" stroke-width="5" stroke-linecap="round"/>'
    if kind=='mushroom':return '<rect x="-6" y="-2" width="12" height="22" rx="5" fill="#EADBBF" stroke="#9C8266"/><path d="M-23 0C-21 -29 21 -29 23 0Q0 11 -23 0Z" fill="#BAA18A" stroke="#8E7967" stroke-width="2"/>'
    if kind in ['plates','bowls','assemble']:
        r='<ellipse cy="4" rx="24" ry="16" fill="#F8EDD8" stroke="#397479" stroke-width="3"/><ellipse cy="2" rx="17" ry="10" fill="none" stroke="#B8C7BA" stroke-width="2"/>'
        if kind=='plates':r='<ellipse cy="10" rx="24" ry="16" fill="#E5DCCB" stroke="#397479" stroke-width="2"/>'+r
        if kind=='bowls':r='<path d="M-24 2Q-21 26 0 25Q21 26 24 2" fill="#EDE1CA" stroke="#397479" stroke-width="3"/>'+r
        if kind=='assemble':r+='<path d="M-16 0-4 -7 -7 6Z M2 -6 15 -2 6 8Z" fill="#D65D46"/><circle cx="-1" cy="4" r="5" fill="#BBCC8D" stroke="#527B46" stroke-width="2"/>'
        return r
    if kind=='prep':return '<rect x="-25" y="-20" width="50" height="36" rx="6" fill="#D0A36F" stroke="#A37950" stroke-width="2"/><path d="M-14 7 4 -13 13 -5 -5 14Z" fill="#CBD2CB" stroke="#6D7D7A"/><path d="M-14 8-20 15" stroke="#435977" stroke-width="7" stroke-linecap="round"/>'
    if kind=='pot':return '<circle r="26" fill="#4D5651"/><rect x="-29" y="-5" width="58" height="13" rx="4" fill="#66503E"/><circle r="20" fill="#A1B3A0" stroke="#F5E9D1" stroke-width="3"/><circle r="15" fill="#D9A84E"/><circle cx="-6" cy="-4" r="4" fill="#DE8436"/><path d="M3 8Q1 -1 10 1L11 6Z" fill="#E6D7B6"/>'
    if kind=='sink':return '<rect x="-25" y="-18" width="50" height="38" rx="8" fill="#CAD7D5" stroke="#74928F" stroke-width="3"/><rect x="-18" y="-11" width="36" height="25" rx="6" fill="#96B6BB"/><path d="M0 -19V-8" stroke="#506C70" stroke-width="6" stroke-linecap="round"/>'
    if kind=='return':return '<rect x="-28" y="-18" width="56" height="39" rx="7" fill="#A1B6BB" stroke="#6B898E" stroke-width="2"/><ellipse rx="19" ry="12" fill="#EDE2CB" stroke="#397479" stroke-width="2"/><path d="M-9 -4 0 1 9 -2" fill="none" stroke="#B88755" stroke-width="4" stroke-linecap="round"/>'
    if kind=='serve':return '<rect x="-26" y="-20" width="52" height="38" rx="6" fill="#F2E7CF" stroke="#397479" stroke-width="3"/><path d="M-15 9Q-15 -11 0 -11Q15 -11 15 9Z" fill="#D7B260"/><circle cy="-14" r="4" fill="#927544"/><path d="M-18 10H18" stroke="#806643" stroke-width="3"/>'
    if kind=='bin':return '<rect x="-21" y="-17" width="42" height="40" rx="9" fill="#B77B61" stroke="#845C49" stroke-width="2"/><ellipse cy="-12" rx="16" ry="6" fill="#745447"/>'
    if kind=='landing':return '<rect x="-25" y="-22" width="50" height="42" rx="6" fill="#E6DCEF" stroke="#8063A8" stroke-width="3" stroke-dasharray="5 4"/><path d="M-10 0H10M0 -10V10" stroke="#8063A8" stroke-width="3"/>'
    return '<rect x="-24" y="-17" width="48" height="34" rx="6" fill="#E8D2AF" stroke="#B8976E" stroke-width="2"/>'
