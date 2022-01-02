<?php
/**
 * Copyright (C) 2022 Stefano Moioli <smxdev4@gmail.com>
 * This software is provided 'as-is', without any express or implied warranty. In no event will the authors be held liable for any damages arising from the use of this software.
 * Permission is granted to anyone to use this software for any purpose, including commercial applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *  1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software. If you use this software in a product, an acknowledgment in the product documentation would be appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

/**
 * adaptation of https://stackoverflow.com/a/4225813/11782802
 */
function hex_dump($data, $newline="\n") {
	static $from = '';
	static $to = '';
	static $width = 16; # number of bytes per line
	static $pad = '.'; # padding for non-visible characters
	
	if ($from==='') {
		for ($i=0; $i<=0xFF; $i++){
			$from .= chr($i);
			$to .= ($i >= 0x20 && $i <= 0x7E) ? chr($i) : $pad;
		}
	}

	// nibbles and spacing
	$line_length = ($width * 2) + $width;

	$hex = str_split(bin2hex($data), $width*2);
	$chars = str_split(strtr($data, $from, $to), $width);

	$offset = 0;
	foreach ($hex as $i => $line){
		//$s_offset = sprintf('%6X',$offset);
		$s_hex = str_pad(
			implode(' ', str_split($line,2)),
			$line_length,
			' ', STR_PAD_RIGHT);

		$s_ascii = str_pad($chars[$i], $width, ' ', STR_PAD_RIGHT);
		//echo "{$s_offset}: {$s_hex} [{$s_ascii}]{$newline}";
		echo "[{$s_ascii}] {$s_hex}{$newline}";
		$offset += $width;
	}
}

function path_concat(string ...$parts){
	return implode(DIRECTORY_SEPARATOR, $parts);
}

function vbs(string $code, string ...$args){
	$tmp = tempnam(sys_get_temp_dir(), 'vbs');
	file_put_contents($tmp, $code);

	try {
		$shArgs = [escapeshellarg($tmp)];
		foreach($args as $a){
			$shArgs[]= escapeshellarg($a);
		}
		$sArgs = implode(' ', $shArgs);
		$output = rtrim(shell_exec("cscript //nologo //e:vbs {$sArgs}"));

		return $output;
	} finally {
		unlink($tmp);
	}
}

$sigrok = path_concat(__DIR__, 'sigrok-cli');
if(PHP_OS_FAMILY === 'Windows') $sigrok .= '.exe';

if(!file_exists($sigrok)){
	if(PHP_OS_FAMILY === 'Windows'){
		$lnk = getenv('APPDATA') . '\Microsoft\Windows\Start Menu\Programs\sigrok\sigrok-cli\sigrok command-line tool.lnk';
		$code = <<<EOS
		set WshShell = WScript.CreateObject("WScript.Shell")
		set Lnk = WshShell.Createshortcut(WScript.Arguments(0))
		WScript.Echo Lnk.WorkingDirectory
		EOS;
		$sigrok = vbs($code, $lnk) . DIRECTORY_SEPARATOR . 'sigrok-cli.exe';
	} else {
		$sigrok = rtrim(shell_exec('which sigrok-cli'));
	}

	if(!file_exists($sigrok)){
		fwrite(STDERR, "Couldn't find sigrok-cli binary\n");
		exit(1);
	}
}

if($argc < 2){
	fwrite(STDERR, "Usage: {$argv[0]} <FakeFTDI.log>\n");
	return 1;
}

$cur_addr = 0;
$cur_buf = '';
$cur_mode = '';

$cmd = escapeshellarg($sigrok)
	. ' -i ' . escapeshellarg($argv[1])
	. ' -I binary:numchannels=2 -C 0=SCL,1=SDA -P i2c -A i2c=addr-data';
$in = popen($cmd, 'r');
if(!is_resource($in)){
	fwrite(STDERR, "failed to run sigrok-cli. command was: '{$cmd}'\n");
	exit(1);
}
while(!feof($in)){
	$line = fgets($in);
	if($line === false) continue;

	$p = array_map('trim', explode(':', $line, 2));
	if(count($p) < 2) continue;

	list($proto, $data) = $p;
	$data = strtolower($data);

	$p = array_map('trim', explode(':', $data, 2));
	if(count($p) < 1) continue;

	$cmd = $p[0];
	switch($cmd){
		case 'start':
			break;
		case 'address read':
			$cur_addr = $p[1];
			$cur_buf = '';
			break;
		case 'address write':
			$cur_addr = $p[1];
			$cur_buf = '';
			break;
		case 'write':
			$cur_mode = 'W';
			break;
		case 'read':
			$cur_mode = 'R';
			break;
		case 'data read':
		case 'data write':
			$cur_buf .= $p[1];
			break;
		case 'stop':
			printf("[{$cur_mode}] {$cur_addr}: ");
			hex_dump(hex2bin($cur_buf));
			break;
	}

}
fclose($in);